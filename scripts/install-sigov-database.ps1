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
    if ($Command) { $args += @('--command', $Command) }
    if ($File) { $args += @('--file', $File) }

    if ($Capture) {
        $output = & $PsqlPath @args 2>&1
        if ($LASTEXITCODE -ne 0) {
            throw "psql falhou no banco '$TargetDatabase' com código $LASTEXITCODE.`n$($output -join [Environment]::NewLine)"
        }
        return ($output -join [Environment]::NewLine).Trim()
    }

    & $PsqlPath @args
    if ($LASTEXITCODE -ne 0) {
        throw "psql falhou no banco '$TargetDatabase' com código $LASTEXITCODE."
    }
}

Assert-SafeIdentifier -Value $Database -Name 'Database'
Assert-SafeIdentifier -Value $MaintenanceDatabase -Name 'MaintenanceDatabase'
if ($ExerciseYear -lt 1900 -or $ExerciseYear -gt 3000) { throw 'ExerciseYear fora do intervalo permitido.' }
if ([string]::IsNullOrWhiteSpace($Password)) {
    throw 'Informe a senha do PostgreSQL em -Password ou na variável PGPASSWORD.'
}

$psqlCommand = Get-Command $PsqlPath -ErrorAction SilentlyContinue
if (-not $psqlCommand) {
    throw "psql não foi encontrado em '$PsqlPath'. Instale o cliente PostgreSQL 16+ ou informe -PsqlPath."
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$rootScript = Join-Path $repoRoot 'script_completop.sql'
$preflightScript = Join-Path $repoRoot 'database/postgres/bootstrap/000_preflight_legacy_compatibility.sql'
$postMigrationScript = Join-Path $repoRoot 'database/postgres/bootstrap/850_post_migration_compatibility.sql'
$bootstrapScript = Join-Path $repoRoot 'database/postgres/bootstrap/900_runtime_bootstrap.sql'

foreach ($requiredFile in @($rootScript, $preflightScript, $postMigrationScript, $bootstrapScript)) {
    if (-not (Test-Path $requiredFile)) { throw "Arquivo obrigatório não encontrado: $requiredFile" }
}

$env:PGPASSWORD = $Password
$createdNow = $false
$generatedPassword = $false

try {
    $serverVersion = Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select current_setting('server_version_num');" -Capture
    $versionNumber = [int](($serverVersion -split '\s+') | Where-Object { $_ -match '^\d+$' } | Select-Object -Last 1)
    if ($versionNumber -lt 160000) {
        throw "PostgreSQL 16 ou superior é obrigatório. server_version_num=$versionNumber"
    }

    $databaseLiteral = ConvertTo-SqlLiteral $Database
    $existsOutput = Invoke-Psql -TargetDatabase $MaintenanceDatabase -Command "select case when exists(select 1 from pg_database where datname = '$databaseLiteral') then '1' else '0' end;" -Capture
    $databaseExists = (($existsOutput -split '\s+') -contains '1')

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
    $generatedScript = Join-Path $generatedDir 'SIGOV_PLUS_ONE_SHOT.generated.sql'

    $parts = @(
        "-- SIGOV+ ONE-SHOT DATABASE INSTALLER`n-- Gerado em $([DateTimeOffset]::Now.ToString('O'))`n-- Banco alvo: $Database`n-- Não contém senha em texto puro.`n",
        Get-Content $preflightScript -Raw,
        "`n-- ===== SCRIPT CONSOLIDADO =====`n",
        Get-Content $rootScript -Raw,
        "`n-- ===== COMPATIBILIDADE PÓS-MIGRATION =====`n",
        Get-Content $postMigrationScript -Raw,
        "`n-- ===== BOOTSTRAP OPERACIONAL =====`n",
        Get-Content $bootstrapScript -Raw
    )
    $oneShot = ($parts -join "`n").Replace("`r`n", "`n")

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
        $oneShot = $oneShot.Replace([string]$entry.Key, [string]$entry.Value)
    }

    if ($oneShot -match '__SIGOV_[A-Z0-9_]+__') {
        throw 'O script gerado ainda contém placeholders de bootstrap não resolvidos.'
    }

    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [IO.File]::WriteAllText($generatedScript, $oneShot, $utf8NoBom)

    Write-Host "Executando instalação completa no banco '$Database'..." -ForegroundColor Cyan
    Invoke-Psql -TargetDatabase $Database -File $generatedScript

    if (-not $NoIdempotencyCheck) {
        Write-Host 'Executando segunda passagem para validar idempotência...' -ForegroundColor Cyan
        Invoke-Psql -TargetDatabase $Database -File $generatedScript
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
    when not exists (select 1 from sigov.perfil_acesso where codigo_externo = 'ADMINISTRADOR_GERAL' and ativo and not is_deleted) then 'FAIL:perfil_admin'
    when not exists (select 1 from sigov.tenant_modulo_contratado where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and ativo) then 'FAIL:modulos'
    when not exists (select 1 from sigov.tenant_configuracao where tenant_id = (select id from sigov.tenant where slug = '$tenantSlugLiteral' limit 1) and chave = 'sistema.bootstrap_concluido' and ativo and not is_deleted) then 'FAIL:parametros'
    else 'OK'
end;
"@
    $validation = Invoke-Psql -TargetDatabase $Database -Command $validationSql -Capture
    if (($validation -split '\s+') -notcontains 'OK') {
        throw "A validação final do bootstrap falhou: $validation"
    }

    Write-Host ''
    Write-Host 'Instalação SIGOV+ concluída com sucesso.' -ForegroundColor Green
    Write-Host "Banco: $Database"
    Write-Host "Tenant: $TenantName ($TenantSlug)"
    Write-Host "Exercício: $ExerciseYear"
    Write-Host "Login inicial: $AdminLogin"
    Write-Host "E-mail inicial: $AdminEmail"
    if ($generatedPassword) {
        Write-Host "Senha temporária gerada: $AdminPassword" -ForegroundColor Yellow
        Write-Host 'Guarde esta senha agora. Ela não foi gravada em texto puro no script ou no banco.' -ForegroundColor Yellow
    } else {
        Write-Host 'Senha temporária: valor informado ao instalador.'
    }
    Write-Host 'A troca da senha é obrigatória no primeiro acesso.' -ForegroundColor Yellow
    Write-Host "Script autônomo gerado: $generatedScript"
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
    $env:PGPASSWORD = $null
}
