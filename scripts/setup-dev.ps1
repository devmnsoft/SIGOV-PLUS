[CmdletBinding()]
param(
    [string]$HostName = 'localhost',
    [int]$Port = 5432,
    [string]$PostgresPassword = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { '123456' }),
    [string]$Database = 'sigov',
    [string]$DatabaseUser = 'sigov',
    [string]$DatabasePassword = $env:SIGOV_DB_PASSWORD,
    [switch]$SkipBuild,
    [switch]$Start
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:ASPNETCORE_ENVIRONMENT -and $env:ASPNETCORE_ENVIRONMENT -ne 'Development') {
    throw 'setup-dev.ps1 é exclusivo de Development e nunca deve ser executado em Production.'
}

$adminPassword = 'SigovDevLocal!2026'
$databasePasswordWasGenerated = [string]::IsNullOrWhiteSpace($DatabasePassword)
if ($databasePasswordWasGenerated) {
    $bytes = [byte[]]::new(30)
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $DatabasePassword = [Convert]::ToBase64String($bytes).Replace('+', 'A').Replace('/', 'B').TrimEnd('=') + '!a9'
}
Write-Host 'Preparando o ambiente local SIGOV+ (banco, migrations e seed Development)...' -ForegroundColor Cyan

& "$PSScriptRoot/setup-local-sigov.ps1" `
    -HostName $HostName `
    -Port $Port `
    -Database $Database `
    -AdminPassword $PostgresPassword `
    -AppDbUser $DatabaseUser `
    -AppDbPassword $DatabasePassword `
    -AdminLogin 'admin' `
    -AdminEmail 'admin@sigov.local' `
    -AdminPasswordApp $adminPassword `
    -WebUrl 'https://localhost:7000' `
    -ApiUrl 'https://localhost:7001' `
    -ResetAdminPassword `
    -SkipBuild:$SkipBuild `
    -StartAfterSetup:$Start `
    -Force

if ($LASTEXITCODE -ne 0) { throw "Provisionamento local falhou com código $LASTEXITCODE." }

# O guard canônico roda sempre depois das migrations; não replica sua lógica em PowerShell.
$previousGuardPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $PostgresPassword
    & psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U postgres -d $Database `
        -c "set sigov.environment = 'DEVELOPMENT'" `
        -f (Join-Path $PSScriptRoot '../database/postgres/seeds/development/999_super_admin_access_guard.sql')
    if ($LASTEXITCODE -ne 0) { throw "Guard administrativo Development falhou com código $LASTEXITCODE." }
}
finally { $env:PGPASSWORD = $previousGuardPassword }

# Não aceite sucesso apenas porque migrations/seed terminaram: confirme o catálogo na
# instância administrativa e uma conexão real usando exatamente a credencial da aplicação.
$previousPgPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $PostgresPassword
    $databaseExists = & psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U postgres -d postgres -Atqc "SELECT 1 FROM pg_database WHERE datname = '$($Database.Replace("'", "''"))';" 2>&1
    if ($LASTEXITCODE -ne 0 -or ($databaseExists -join '').Trim() -ne '1') {
        throw "O banco '$Database' não existe em ${HostName}:$Port após o provisionamento."
    }
    $env:PGPASSWORD = $DatabasePassword
    $currentDatabase = & psql -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $DatabaseUser -d $Database -Atqc 'select current_database();' 2>&1
    if ($LASTEXITCODE -ne 0 -or ($currentDatabase -join '').Trim() -ne $Database) {
        throw "O usuário '$DatabaseUser' não conseguiu validar o banco '$Database' em ${HostName}:$Port."
    }
}
finally { $env:PGPASSWORD = $previousPgPassword }

& "$PSScriptRoot/check-local-db.ps1" -HostName $HostName -Port $Port -Database $Database -User $DatabaseUser -Password $DatabasePassword -MaintenancePassword $PostgresPassword
if ($LASTEXITCODE -ne 0) { throw "check-local-db.ps1 falhou com código $LASTEXITCODE." }

$previousSigovDbPassword = $env:SIGOV_DB_PASSWORD
try {
    $env:SIGOV_DB_PASSWORD = $DatabasePassword
    & "$PSScriptRoot/check-local-login.ps1" -HostName $HostName -Port $Port -Database $Database -User $DatabaseUser -Login admin -Password $adminPassword
    if ($LASTEXITCODE -ne 0) { throw "check-local-login.ps1 falhou para admin com código $LASTEXITCODE." }
    & "$PSScriptRoot/check-local-login.ps1" -HostName $HostName -Port $Port -Database $Database -User $DatabaseUser -Login superadmin -Password 'SigovSuperAdmin!2026'
    if ($LASTEXITCODE -ne 0) { throw "check-local-login.ps1 falhou para superadmin com código $LASTEXITCODE." }
}
finally { $env:SIGOV_DB_PASSWORD = $previousSigovDbPassword }

Write-Host ''
Write-Host 'Ambiente Development provisionado.' -ForegroundColor Green
Write-Host 'Web:     https://localhost:7000/Auth/Login'
Write-Host 'Swagger: https://localhost:7001/swagger'
Write-Host 'Login:   admin'
Write-Host 'Senha:   SigovDevLocal!2026'
Write-Host 'A credencial acima é exclusiva do ambiente local e não é usada em Production.' -ForegroundColor Yellow
if ($databasePasswordWasGenerated) {
    Write-Host 'A senha do usuário de banco foi gerada aleatoriamente e persistida somente no .env.local ignorado pelo Git.' -ForegroundColor Yellow
}
