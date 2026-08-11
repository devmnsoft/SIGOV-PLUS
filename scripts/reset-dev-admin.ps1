[CmdletBinding()]
param(
    [string]$HostName = 'localhost', [int]$Port = 5432, [string]$Database = 'sigov',
    [string]$User = 'sigov', [string]$PostgresUser = 'postgres',
    [string]$PostgresPassword = $(if ($env:PGPASSWORD) { $env:PGPASSWORD } else { '123456' }),
    [string]$PsqlPath = 'psql'
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($env:ASPNETCORE_ENVIRONMENT -and $env:ASPNETCORE_ENVIRONMENT -ne 'Development') {
    throw 'reset-dev-admin.ps1 é bloqueado fora de Development.'
}
if ([string]::IsNullOrWhiteSpace($env:SIGOV_DB_PASSWORD)) { throw 'Defina SIGOV_DB_PASSWORD com a senha do usuário de banco local.' }

$previousPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $PostgresPassword
    & $PsqlPath -X -v ON_ERROR_STOP=1 -h $HostName -p $Port -U $PostgresUser -d $Database `
        -c "set sigov.environment = 'DEVELOPMENT'" `
        -f (Join-Path $PSScriptRoot '../database/postgres/seeds/development/999_super_admin_access_guard.sql')
    if ($LASTEXITCODE -ne 0) { throw "Guard administrativo falhou com código $LASTEXITCODE." }
    $env:PGPASSWORD = $previousPassword
    & "$PSScriptRoot/check-local-login.ps1" -HostName $HostName -Port $Port -Database $Database -User $User -Login admin -Password 'SigovDevLocal!2026' -PsqlPath $PsqlPath
    if ($LASTEXITCODE -ne 0) { throw "Validação do admin falhou com código $LASTEXITCODE." }
    & "$PSScriptRoot/check-local-login.ps1" -HostName $HostName -Port $Port -Database $Database -User $User -Login superadmin -Password 'SigovSuperAdmin!2026' -PsqlPath $PsqlPath
    if ($LASTEXITCODE -ne 0) { throw "Validação do superadmin falhou com código $LASTEXITCODE." }
}
finally { $env:PGPASSWORD = $previousPassword }
Write-Host 'Login: admin'
Write-Host 'Senha: SigovDevLocal!2026'
Write-Host 'Status: válido' -ForegroundColor Green
