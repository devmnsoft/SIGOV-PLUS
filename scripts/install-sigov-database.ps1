[CmdletBinding()]
param(
    [string]$HostName = 'localhost',
    [int]$Port = 5432,
    [string]$Database = 'sigov',
    [string]$MaintenanceDatabase = 'postgres',
    [string]$User = 'postgres',
    [string]$Password = $env:PGPASSWORD,
    [string]$AdminLogin = 'admin',
    [string]$AdminEmail = 'admin@sigov.local',
    [string]$AdminName = 'Administrador Geral',
    [string]$AdminPassword = $env:SIGOV_BOOTSTRAP_ADMIN_PASSWORD,
    [string]$TenantName = 'SIGOV Administração',
    [string]$TenantSlug = 'sigov-local',
    [string]$TenantDocument = '00000000000191',
    [string]$EntityName = 'Entidade Principal',
    [string]$EntityCnpj = '00000000000000',
    [ValidateSet('DEVELOPMENT','HOMOLOGATION','PRODUCTION')]
    [string]$Environment = 'DEVELOPMENT',
    [int]$ExerciseYear = (Get-Date).Year,
    [string]$PsqlPath = 'psql',
    [switch]$Recreate,
    [switch]$ResetAdminPassword,
    [switch]$NoIdempotencyCheck,
    [switch]$KeepFailedDatabase,
    [switch]$RunDiagnosticsBefore,
    [switch]$RunDiagnosticsAfter = $true,
    [switch]$RepairBeforeInstall,
    [switch]$RepairAfterInstall,
    [switch]$Quiet,
    [switch]$VerboseSql,
    [switch]$FailOnWarnings
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeIdentifier {
    param([Parameter(Mandatory)][string]$Value, [Parameter(Mandatory)][string]$Name)
    if ($Value -notmatch '^[A-Za-z_][A-Za-z0-9_\-]{0,62}$') {
        throw "$Name inválido. Use somente letras, números, sublinhado e hífen, iniciando por letra ou sublinhado."
    }
}

function Assert-RequiredText {
    param([AllowEmptyString()][string]$Value, [Parameter(Mandatory)][string]$Name)
    if ([string]::IsNullOrWhiteSpace($Value)) { throw "$Name é obrigatório." }
}

function ConvertTo-SqlLiteral {
    param([AllowEmptyString()][string]$Value)
    if ($null -eq $Value) { return '' }
    return $Value.Replace("'", "''")
}

function Quote-PgIdentifier {
    param([Parameter(Mandatory)][string]$Value)
    return '"' + $Value.Replace('"', '""') + '"'
}

function New-RandomPassword {
    param([int]$Length = 24)
    $alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@$%*-_'
    $bytes = [byte[]]::new($Length)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $chars = for ($i = 0; $i -lt $Length; $i++) {
        $alphabet[$bytes[$i] % $alphabet.Length]
    }
    return -join $chars
}

function New-SigovPasswordHash {
    param([Parameter(Mandatory)][string]$PlainText)
    $iterations = 210000
    # Deve permanecer compatível com PasswordHashService (salt de 16 bytes).
    $salt = [byte[]]::new(16)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($salt)
    $derive = [System.Security.Cryptography.Rfc2898DeriveBytes]::new(
        $PlainText,
        $salt,
        $iterations,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    try {
        $hash = $derive.GetBytes(32)
        return 'SIGOV_PBKDF2_V1${0}${1}${2}' -f $iterations, [Convert]::ToBase64String($salt), [Convert]::ToBase64String($hash)
    }
    finally {
        $derive.Dispose()
    }
}

function Invoke-Psql {
    param(
        [Parameter(Mandatory)][string]$TargetDatabase,
        [string]$Command,
        [string]$File,
        [switch]$Capture
    )

    $arguments = @(
        '-X',
        '--set', 'ON_ERROR_STOP=1',
        '--host', $HostName,
        '--port', $Port.ToString(),
        '--username', $User,
        '--dbname', $TargetDatabase
    )
    if ($Capture) { $arguments += @('--tuples-only', '--no-align') }
    if ($Command) { $arguments += @('--command', $Command) }
    if ($File) { $arguments += @('--file', $File) }

    if (-not $VerboseSql) { $arguments += @('--set', 'VERBOSITY=terse') }
    $output = & $PsqlPath @arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "psql falhou no banco '$TargetDatabase' com código $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }
    if ($Capture) { return ($output -join [Environment]::NewLine).Trim() }
    if (-not $Quiet) { $output | Where-Object { $VerboseSql -or $_ -notmatch 'already exists, skipping|já existe' } | ForEach-Object { Write-Host $_ } }
}

function Invoke-OperationalScript {
    param([Parameter(Mandatory)][string]$Path, [string[]]$AdditionalArguments=@(), [int[]]$AcceptedExitCodes=@(0))
    $pwsh = (Get-Process -Id $PID).Path
    & $pwsh -NoProfile -File $Path -HostName $HostName -Port $Port -Database $Database -MaintenanceDatabase $MaintenanceDatabase -User $User -PsqlPath $PsqlPath @AdditionalArguments
    if ($LASTEXITCODE -notin $AcceptedExitCodes) { throw "$(Split-Path $Path -Leaf) encerrou com código $LASTEXITCODE." }
    return $LASTEXITCODE
}

function Invoke-ManifestInstallation {
    param([Parameter(Mandatory)][string]$ScriptPath)
    & $ScriptPath -HostName $HostName -Port $Port -Database $Database -User $User -PsqlPath $PsqlPath
    if ($LASTEXITCODE -ne 0) { throw "O aplicador de migrations encerrou com código $LASTEXITCODE." }
}

Assert-SafeIdentifier -Value $Database -Name 'Database'
Assert-SafeIdentifier -Value $MaintenanceDatabase -Name 'MaintenanceDatabase'
Assert-RequiredText -Value $AdminLogin -Name 'AdminLogin'
Assert-RequiredText -Value $AdminName -Name 'AdminName'
Assert-RequiredText -Value $TenantName -Name 'TenantName'
Assert-RequiredText -Value $TenantSlug -Name 'TenantSlug'
Assert-RequiredText -Value $EntityName -Name 'EntityName'
Assert-RequiredText -Value $EntityCnpj -Name 'EntityCnpj'
if ($AdminEmail -notmatch '^[^@\s]+@[^@\s]+\.[^@\s]+$') { throw 'AdminEmail inválido.' }
if ($ExerciseYear -lt 1900 -or $ExerciseYear -gt 3000) { throw 'ExerciseYear fora do intervalo permitido.' }
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw 'Informe a senha do PostgreSQL em -Password ou na variável PGPASSWORD.'
}
if (-not (Get-Command $PsqlPath -ErrorAction SilentlyContinue)) {
    throw "psql não foi encontrado em '$PsqlPath'. Instale o cliente PostgreSQL 16+ ou informe -PsqlPath."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$manifestInstaller = Join-Path $repoRoot 'scripts/apply-migrations-manifest.ps1'
$bootstrapTemplate = Join-Path $repoRoot 'database/postgres/bootstrap/900_runtime_bootstrap.sql'
$diagnosticScript = Join-Path $repoRoot 'scripts/diagnose-sigov-database.ps1'
$repairScript = Join-Path $repoRoot 'scripts/repair-sigov-database.ps1'
foreach ($requiredFile in @($manifestInstaller, $bootstrapTemplate, $diagnosticScript, $repairScript)) {
    if (-not (Test-Path $requiredFile)) { throw "Arquivo obrigatório não encontrado: $requiredFile" }
}

$previousPgPassword = $env:PGPASSWORD
$env:PGPASSWORD = $Password
$createdNow = $false
$generatedPassword = $false
$credentialProvisioned = $false
$resolvedBootstrap = $null

try {
    $serverVersion = Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select current_setting('server_version_num');" -Capture
    if ([int]$serverVersion -lt 160000) {
        throw "PostgreSQL 16 ou superior é obrigatório. server_version_num=$serverVersion"
    }

    $databaseLiteral = ConvertTo-SqlLiteral $Database
    $databaseExists = (Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select case when exists(select 1 from pg_database where datname = '$databaseLiteral') then '1' else '0' end;" -Capture) -eq '1'

    if ($RunDiagnosticsBefore -and $databaseExists) { Invoke-OperationalScript $diagnosticScript -AcceptedExitCodes @(0,1) | Out-Null }
    if ($RepairBeforeInstall -and $databaseExists) { Invoke-OperationalScript $repairScript -AdditionalArguments @('-Apply','-Confirm:$false') | Out-Null }

    if ($Recreate -and $databaseExists) {
        Write-Host "Encerrando conexões e recriando o banco '$Database'..." -ForegroundColor Yellow
        Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select pg_terminate_backend(pid) from pg_stat_activity where datname = '$databaseLiteral' and pid <> pg_backend_pid();"
        Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "drop database if exists $(Quote-PgIdentifier $Database);"
        $databaseExists = $false
    }

    if (-not $databaseExists) {
        Write-Host "Criando o banco '$Database' em UTF-8..." -ForegroundColor Cyan
        Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "create database $(Quote-PgIdentifier $Database) with encoding 'UTF8' template template0;"
        $createdNow = $true
    }

    Write-Host "Aplicando todas as migrations ordenadas no banco '$Database'..." -ForegroundColor Cyan
    Invoke-ManifestInstallation -ScriptPath $manifestInstaller

    $adminLoginLiteral = ConvertTo-SqlLiteral $AdminLogin
    $adminEmailLiteral = ConvertTo-SqlLiteral $AdminEmail
    $tenantSlugLiteral = ConvertTo-SqlLiteral $TenantSlug
    $adminStateSql = @"
select coalesce((
    select jsonb_build_object(
        'id', u.id,
        'senhaHash', u.senha_hash,
        'senhaDeveSerAlterada', coalesce(u.senha_deve_ser_alterada, false),
        'deveAlterarSenha', coalesce(u.deve_alterar_senha, false)
    )::text
      from sigov.usuario u
      join sigov.tenant t on t.id = u.tenant_id
     where t.slug = '$tenantSlugLiteral'
       and (lower(u.login) = lower('$adminLoginLiteral') or lower(u.email) = lower('$adminEmailLiteral'))
       and u.is_deleted = false
     order by u.id
     limit 1
), '');
"@
    $adminStateRaw = Invoke-Psql -TargetDatabase $Database -Command $adminStateSql -Capture
    $adminState = $null
    if (-not [string]::IsNullOrWhiteSpace($adminStateRaw)) {
        $adminState = $adminStateRaw | ConvertFrom-Json
    }

    $existingHashCompatible = $false
    if ($null -ne $adminState -and -not [string]::IsNullOrWhiteSpace([string]$adminState.senhaHash)) {
        $existingHashCompatible = ([string]$adminState.senhaHash).StartsWith('SIGOV_PBKDF2_V1$', [StringComparison]::Ordinal)
    }

    if ($null -ne $adminState -and $existingHashCompatible -and -not $ResetAdminPassword) {
        $adminHash = [string]$adminState.senhaHash
        $adminMustChange = [bool]$adminState.senhaDeveSerAlterada -or [bool]$adminState.deveAlterarSenha
        if (-not [string]::IsNullOrWhiteSpace($AdminPassword)) {
            Write-Warning 'AdminPassword foi ignorada porque o administrador já possui hash válido. Use -ResetAdminPassword para redefini-la.'
        }
    }
    else {
        if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
            $AdminPassword = New-RandomPassword
            $generatedPassword = $true
        }
        if ($AdminPassword.Length -lt 12) {
            throw 'A senha administrativa inicial deve possuir pelo menos 12 caracteres.'
        }
        $adminHash = New-SigovPasswordHash -PlainText $AdminPassword
        # O reset explícito de Development é uma credencial operacional conhecida e
        # deve permitir o redirecionamento direto para MinhaCentral. Nos demais casos,
        # a senha inicial continua temporária e exige troca.
        $adminMustChange = -not ($ResetAdminPassword -and $Environment -eq 'DEVELOPMENT')
        $credentialProvisioned = $true
    }

    $bootstrap = (Get-Content $bootstrapTemplate -Raw).Replace("`r`n", "`n")
    $replacements = [ordered]@{
        '__SIGOV_TENANT_NAME__' = ConvertTo-SqlLiteral $TenantName
        '__SIGOV_TENANT_SLUG__' = $tenantSlugLiteral
        '__SIGOV_TENANT_DOCUMENT__' = ConvertTo-SqlLiteral $TenantDocument
        '__SIGOV_ENTITY_NAME__' = ConvertTo-SqlLiteral $EntityName
        '__SIGOV_ENTITY_CNPJ__' = ConvertTo-SqlLiteral $EntityCnpj
        '__SIGOV_ADMIN_NAME__' = ConvertTo-SqlLiteral $AdminName
        '__SIGOV_ADMIN_LOGIN__' = $adminLoginLiteral
        '__SIGOV_ADMIN_EMAIL__' = $adminEmailLiteral
        '__SIGOV_ADMIN_PASSWORD_HASH__' = ConvertTo-SqlLiteral $adminHash
        '__SIGOV_ADMIN_MUST_CHANGE__' = $adminMustChange.ToString().ToLowerInvariant()
        '__SIGOV_ENVIRONMENT__' = ConvertTo-SqlLiteral $Environment
        '__SIGOV_CURRENT_YEAR__' = $ExerciseYear.ToString([Globalization.CultureInfo]::InvariantCulture)
    }
    foreach ($entry in $replacements.GetEnumerator()) {
        $bootstrap = $bootstrap.Replace([string]$entry.Key, [string]$entry.Value)
    }
    if ($bootstrap -match '__SIGOV_[A-Z0-9_]+__') {
        throw 'O bootstrap gerado ainda contém placeholders não resolvidos.'
    }

    $resolvedBootstrap = Join-Path ([IO.Path]::GetTempPath()) ("sigov-bootstrap-{0}.sql" -f [Guid]::NewGuid().ToString('N'))
    [IO.File]::WriteAllText($resolvedBootstrap, $bootstrap, [Text.UTF8Encoding]::new($false))

    Write-Host 'Criando tenant, entidade, exercício, parâmetros, módulos, permissões e usuário inicial...' -ForegroundColor Cyan
    Invoke-Psql -TargetDatabase $Database -File $resolvedBootstrap

    if (-not $NoIdempotencyCheck) {
        Write-Host 'Executando segunda passagem para validar idempotência...' -ForegroundColor Cyan
        Invoke-ManifestInstallation -ScriptPath $manifestInstaller
        Invoke-Psql -TargetDatabase $Database -File $resolvedBootstrap
    }

    $validationSql = @"
select case
    when to_regnamespace('sigov') is null then 'FAIL:schema_sigov'
    when to_regclass('sigov.usuario') is null then 'FAIL:tabela_usuario'
    when to_regclass('sigov.tenant') is null then 'FAIL:tabela_tenant'
    when not exists (select 1 from sigov.tenant where slug = '$tenantSlugLiteral' and ativo and not is_deleted) then 'FAIL:tenant_bootstrap'
    when not exists (select 1 from sigov.entidade where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and ativo and not is_deleted) then 'FAIL:entidade_bootstrap'
    when not exists (select 1 from sigov.exercicio where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and ano = $ExerciseYear and ativo and not is_deleted) then 'FAIL:exercicio_bootstrap'
    when not exists (select 1 from sigov.usuario where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and lower(login) = lower('$adminLoginLiteral') and ativo and not is_deleted and senha_hash like 'SIGOV_PBKDF2_V1$%') then 'FAIL:usuario_admin'
    when not exists (select 1 from sigov.perfil_acesso where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and codigo_externo = 'ADMINISTRADOR_GERAL' and ativo and not is_deleted) then 'FAIL:perfil_admin'
    when not exists (
        select 1
          from sigov.usuario u
          join sigov.usuario_grupo ug on ug.usuario_id = u.id and not ug.is_deleted
          join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id and not gp.is_deleted
          join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id and pa.codigo_externo = 'ADMINISTRADOR_GERAL' and not pa.is_deleted
         where u.tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1)
           and lower(u.login) = lower('$adminLoginLiteral')
    ) then 'FAIL:vinculo_admin'
    when not exists (select 1 from sigov.tenant_modulo_contratado where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and ativo) then 'FAIL:modulos'
    when not exists (select 1 from sigov.tenant_configuracao where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and chave = 'sistema.bootstrap_concluido' and ativo and not is_deleted) then 'FAIL:parametros'
    else 'OK'
end;
"@
    $validation = Invoke-Psql -TargetDatabase $Database -Command $validationSql -Capture
    if ($validation -ne 'OK') { throw "A validação final do bootstrap falhou: $validation" }

    if ($RepairAfterInstall) { Invoke-OperationalScript $repairScript -AdditionalArguments @('-Apply','-Confirm:$false') | Out-Null }
    $diagnosticExit = 0
    if ($RunDiagnosticsAfter) { $diagnosticExit = Invoke-OperationalScript $diagnosticScript -AcceptedExitCodes @(0,1); if ($FailOnWarnings -and $diagnosticExit -ne 0) { throw 'Diagnóstico final encontrou avisos ou erros e -FailOnWarnings está ativo.' } }

    $summaryRaw = Invoke-Psql -TargetDatabase $Database -Capture -Command "select jsonb_build_object('modules',(select count(*) from sigov.tenant_modulo_contratado where ativo),'permissions',(select count(*) from sigov.permissao where ativo and not is_deleted))::text;"
    $summary = $summaryRaw | ConvertFrom-Json

    $resultDir = Join-Path $repoRoot 'artifacts/database'
    New-Item -ItemType Directory -Force -Path $resultDir | Out-Null
    $resultPath = Join-Path $resultDir 'install-result.json'
    [ordered]@{
        status = 'success'
        database = $Database
        tenantSlug = $TenantSlug
        adminLogin = $AdminLogin
        exerciseYear = $ExerciseYear
        credentialProvisioned = $credentialProvisioned
        idempotencyChecked = -not $NoIdempotencyCheck
        databaseCreated = $createdNow
        adminState = if ($credentialProvisioned) { if ($ResetAdminPassword) {'reset'} else {'created-or-upgraded'} } else {'preserved'}
        tenantState = if ($createdNow) {'created'} else {'preserved'}
        modules = [int]$summary.modules
        permissions = [int]$summary.permissions
        diagnostics = if ($RunDiagnosticsAfter) { if ($diagnosticExit -eq 0) {'healthy'} else {'attention'} } else {'not-run'}
        completedAt = [DateTimeOffset]::Now.ToString('O')
    } | ConvertTo-Json | Set-Content -Path $resultPath -Encoding UTF8

    Write-Host ''
    Write-Host 'Instalação SIGOV+ concluída com sucesso.' -ForegroundColor Green
    Write-Host "Banco: $Database"
    Write-Host "Tenant: $TenantName ($TenantSlug)"
    Write-Host "Exercício: $ExerciseYear"
    Write-Host "Login inicial: $AdminLogin"
    Write-Host "E-mail inicial: $AdminEmail"
    if ($credentialProvisioned -and $generatedPassword) {
        Write-Host "Senha temporária gerada: $AdminPassword" -ForegroundColor Yellow
        Write-Host 'Guarde esta senha agora. Ela não foi gravada em texto puro no banco ou no repositório.' -ForegroundColor Yellow
    }
    elseif ($credentialProvisioned) {
        Write-Host 'Senha temporária: valor informado ao instalador.'
    }
    else {
        Write-Host 'A credencial administrativa existente foi preservada.'
    }
    if ($adminMustChange) {
        Write-Host 'A troca da senha está marcada como obrigatória no próximo acesso.' -ForegroundColor Yellow
    }
    Write-Host "Relatório: $resultPath"
    Write-Host "Resumo: banco=$(if($createdNow){'criado'}else{'reaproveitado'}); admin=$(if($credentialProvisioned){'provisionado/resetado'}else{'preservado'}); tenant=$(if($createdNow){'criado'}else{'preservado'}); módulos=$($summary.modules); permissões=$($summary.permissions); status=SUCESSO"
}
catch {
    if ($createdNow -and -not $KeepFailedDatabase) {
        try {
            $databaseLiteral = ConvertTo-SqlLiteral $Database
            Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select pg_terminate_backend(pid) from pg_stat_activity where datname = '$databaseLiteral' and pid <> pg_backend_pid();"
            Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "drop database if exists $(Quote-PgIdentifier $Database);"
            Write-Warning "O banco recém-criado '$Database' foi removido porque a instalação falhou."
        }
        catch {
            Write-Warning "Não foi possível remover o banco incompleto '$Database': $($_.Exception.Message)"
        }
    }
    Write-Error "Instalação interrompida: $($_.Exception.Message)`nDiagnóstico recomendado: ./scripts/diagnose-sigov-database.ps1 -Database '$Database'`nReparo seguro: ./scripts/repair-sigov-database.ps1 -Database '$Database' -WhatIf`nLogs e relatórios: artifacts/database/"
    throw
}
finally {
    if ($resolvedBootstrap -and (Test-Path $resolvedBootstrap)) {
        Remove-Item $resolvedBootstrap -Force -ErrorAction SilentlyContinue
    }
    $env:PGPASSWORD = $previousPgPassword
}
