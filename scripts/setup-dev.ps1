[CmdletBinding()]
param(
    [string]$PostgresPassword = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { '123456' }),
    [string]$Database = 'sigov',
    [string]$DatabaseUser = 'sigov',
    [string]$DatabasePassword = 'change_me',
    [switch]$SkipBuild,
    [switch]$Start
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($env:ASPNETCORE_ENVIRONMENT -and $env:ASPNETCORE_ENVIRONMENT -ne 'Development') {
    throw 'setup-dev.ps1 é exclusivo de Development e nunca deve ser executado em Production.'
}

$adminPassword = 'SigovDevLocal!2026'
Write-Host 'Preparando o ambiente local SIGOV+ (banco, migrations e seed Development)...' -ForegroundColor Cyan

& "$PSScriptRoot/setup-local-sigov.ps1" `
    -Database $Database `
    -AdminPassword $PostgresPassword `
    -AppDbUser $DatabaseUser `
    -AppDbPassword $DatabasePassword `
    -AdminLogin 'admin' `
    -AdminEmail 'admin@sigov.local' `
    -AdminPasswordApp $adminPassword `
    -ResetAdminPassword `
    -SkipBuild:$SkipBuild `
    -StartAfterSetup:$Start `
    -Force

if ($LASTEXITCODE -ne 0) { throw "Provisionamento local falhou com código $LASTEXITCODE." }

Write-Host ''
Write-Host 'Ambiente Development provisionado.' -ForegroundColor Green
Write-Host 'Web:     https://localhost:7000/Auth/Login'
Write-Host 'Swagger: https://localhost:7001/swagger'
Write-Host 'Login:   admin'
Write-Host 'Senha:   SigovDevLocal!2026'
Write-Host 'A credencial acima é exclusiva do ambiente local e não é usada em Production.' -ForegroundColor Yellow
