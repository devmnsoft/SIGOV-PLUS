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
    [switch]$NoIdempotencyCheck,
    [switch]$KeepFailedDatabase
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Assert-SafeIdentifier {
    param([Parameter(Mandatory)][string]$Value, [Parameter(Mandatory)][string]$Name)
    if ($Value -notmatch '^[A-Za-z_][A-Za-z0-9_\-]{0,62}$') {
        throw "$Name inválido. Use somente letras, números, sublinhado e hífen, iniciando por letra ou sublinhado."
    }
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
    $salt = [byte[]]::new(24)
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

    $args = @(
        '-X',
        '--set', 'ON_ERROR_STOP=1',
        '--host', $HostName,
        '--port', $Port.ToString(),
        '--username', $User,
        '--dbname', $TargetDatabase
    )
    if ($Capture) { $args += @('--tuples-only', '--no-align') }
    if ($Command) { $args += @('--command', $Command) }
    if ($File) { $args += @('--file', $File) }

    $output = & $PsqlPath @args 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "psql falhou no banco '$TargetDatabase' com código $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
    }
    if ($Capture) { return ($output -join [Environment]::NewLine).Trim() }
    $output | ForEach-Object { Write-Host $_ }
}

function Invoke-ManifestInstallation {
    param([Parameter(Mandatory)][string]$ScriptPath)
    & $ScriptPath -HostName $HostName -Port $Port -Database $Database -User $User
    if ($LASTEXITCODE -ne 0) { throw "O aplicador de migrations encerrou com código $LASTEXITCODE." }
}

Assert-SafeIdentifier -Value $Database -Name 'Database'
Assert-SafeIdentifier -Value $MaintenanceDatabase -Name 'MaintenanceDatabase'
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
foreach ($requiredFile in @($manifestInstaller, $bootstrapTemplate)) {
    if (-not (Test-Path $requiredFile)) { throw "Arquivo obrigatório não encontrado: $requiredFile" }
}

$previousPgPassword = $env:PGPASSWORD
$env:PGPASSWORD = $Password
$createdNow = $false
$generatedPassword = $false

try {
    $serverVersion = Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select current_setting('server_version_num');" -Capture
    if ([int]$serverVersion -lt 160000) {
        throw "PostgreSQL 16 ou superior é obrigatório. server_version_num=$serverVersion"
    }

    $databaseLiteral = ConvertTo-SqlLiteral $Database
    $databaseExists = (Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select case when exists(select 1 from pg_database where datname = '$databaseLiteral') then '1' else '0' end;" -Capture) -eq '1'

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

    if ([string]::IsNullOrWhiteSpace($AdminPassword)) {
        $AdminPassword = New-RandomPassword
        $generatedPassword = $true
    }
    if ($AdminPassword.Length -lt 12) {
        throw 'A senha administrativa inicial deve possuir pelo menos 12 caracteres.'
    }

    $adminHash = New-SigovPasswordHash -PlainText $AdminPassword
    $generatedDir = Join-Path $repoRoot 'artifacts/database'
    New-Item -ItemType Directory -Force -Path $generatedDir | Out-Null
    $resolvedBootstrap = Join-Path $generatedDir 'SIGOV_PLUS_BOOTSTRAP.generated.sql'
    $bootstrap = (Get-Content $bootstrapTemplate -Raw).Replace("`r`n", "`n")

    $replacements = [ordered]@{
        '__SIGOV_TENANT_NAME__' = ConvertTo-SqlLiteral $TenantName
        '__SIGOV_TENANT_SLUG__' = ConvertTo-SqlLiteral $TenantSlug
        '__SIGOV_TENANT_DOCUMENT__' = ConvertTo-SqlLiteral $TenantDocument
        '__SIGOV_ENTITY_NAME__' = ConvertTo-SqlLiteral $EntityName
        '__SIGOV_ENTITY_CNPJ__' = ConvertTo-SqlLiteral $EntityCnpj
        '__SIGOV_ADMIN_NAME__' = ConvertTo-SqlLiteral $AdminName
        '__SIGOV_ADMIN_LOGIN__' = ConvertTo-SqlLiteral $AdminLogin
        '__SIGOV_ADMIN_EMAIL__' = ConvertTo-SqlLiteral $AdminEmail
        '__SIGOV_ADMIN_PASSWORD_HASH__' = ConvertTo-SqlLiteral $adminHash
        '__SIGOV_ENVIRONMENT__' = ConvertTo-SqlLiteral $Environment
        '__SIGOV_CURRENT_YEAR__' = $ExerciseYear.ToString([Globalization.CultureInfo]::InvariantCulture)
    }
    foreach ($entry in $replacements.GetEnumerator()) {
        $bootstrap = $bootstrap.Replace([string]$entry.Key, [string]$entry.Value)
    }
    if ($bootstrap -match '__SIGOV_[A-Z0-9_]+__') {
        throw 'O bootstrap gerado ainda contém placeholders não resolvidos.'
    }
    [IO.File]::WriteAllText($resolvedBootstrap, $bootstrap, [Text.UTF8Encoding]::new($false))

    Write-Host "Aplicando todas as migrations ordenadas no banco '$Database'..." -ForegroundColor Cyan
    Invoke-ManifestInstallation -ScriptPath $manifestInstaller
    Write-Host 'Criando tenant, entidade, exercício, parâmetros, módulos, permissões e usuário inicial...' -ForegroundColor Cyan
    Invoke-Psql -TargetDatabase $Database -File $resolvedBootstrap

    if (-not $NoIdempotencyCheck) {
        Write-Host 'Executando segunda passagem para validar idempotência...' -ForegroundColor Cyan
        Invoke-ManifestInstallation -ScriptPath $manifestInstaller
        Invoke-Psql -TargetDatabase $Database -File $resolvedBootstrap
    }

    $adminLoginLiteral = ConvertTo-SqlLiteral $AdminLogin
    $tenantSlugLiteral = ConvertTo-SqlLiteral $TenantSlug
    $validationSql = @"
select case
    when to_regnamespace('sigov') is null then 'FAIL:schema_sigov'
    when to_regclass('sigov.usuario') is null then 'FAIL:tabela_usuario'
    when to_regclass('sigov.tenant') is null then 'FAIL:tabela_tenant'
    when not exists (select 1 from sigov.tenant where slug = '$tenantSlugLiteral' and ativo and not is_deleted) then 'FAIL:tenant_bootstrap'
    when not exists (select 1 from sigov.usuario where lower(login) = lower('$adminLoginLiteral') and ativo and not is_deleted and senha_hash like 'SIGOV_PBKDF2_V1$%') then 'FAIL:usuario_admin'
    when not exists (select 1 from sigov.perfil_acesso where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and codigo_externo = 'ADMINISTRADOR_GERAL' and ativo and not is_deleted) then 'FAIL:perfil_admin'
    when not exists (select 1 from sigov.tenant_modulo_contratado where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and ativo) then 'FAIL:modulos'
    when not exists (select 1 from sigov.tenant_configuracao where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and chave = 'sistema.bootstrap_concluido' and ativo and not is_deleted) then 'FAIL:parametros'
    else 'OK'
end;
"@
    $validation = Invoke-Psql -TargetDatabase $Database -Command $validationSql -Capture
    if ($validation -ne 'OK') { throw "A validação final do bootstrap falhou: $validation" }

    Write-Host ''
    Write-Host 'Instalação SIGOV+ concluída com sucesso.' -ForegroundColor Green
    Write-Host "Banco: $Database"
    Write-Host "Tenant: $TenantName ($TenantSlug)"
    Write-Host "Exercício: $ExerciseYear"
    Write-Host "Login inicial: $AdminLogin"
    Write-Host "E-mail inicial: $AdminEmail"
    if ($generatedPassword) {
        Write-Host "Senha temporária gerada: $AdminPassword" -ForegroundColor Yellow
        Write-Host 'Guarde esta senha agora. Ela não foi gravada em texto puro no banco ou no repositório.' -ForegroundColor Yellow
    } else {
        Write-Host 'Senha temporária: valor informado ao instalador.'
    }
    Write-Host 'A troca da senha é obrigatória no primeiro acesso.' -ForegroundColor Yellow
    Write-Host "Bootstrap resolvido: $resolvedBootstrap"
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
    throw
}
finally {
    $env:PGPASSWORD = $previousPgPassword
}
