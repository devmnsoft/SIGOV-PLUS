param(
  [string]$Environment = $env:ASPNETCORE_ENVIRONMENT,
  [switch]$SkipDocker,
  [switch]$SkipMigrations
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Environment)) { $Environment = 'Homologation' }
if ($Environment -eq 'Production') { throw 'Homologação não pode ser preparada em Production.' }
$tenant = if ($env:SIGOV_HML_TENANT_SLUG) { $env:SIGOV_HML_TENANT_SLUG } else { 'homologacao' }
$email = if ($env:SIGOV_HML_ADMIN_EMAIL) { $env:SIGOV_HML_ADMIN_EMAIL } else { 'admin.hml@sigov.local' }
if (-not $SkipDocker) { docker compose up -d sigov-postgres }
if (-not $SkipMigrations) { Write-Host 'INFO migrations devem ser aplicadas pelo Sigov.Api com RunMigrationsOnStartup ou runner operacional.' }
Write-Host "Tenant homologação: $tenant"
Write-Host "Admin homologação: $email"
if ($env:SIGOV_HML_ADMIN_PASSWORD) { Write-Host 'Senha de homologação fornecida por SIGOV_HML_ADMIN_PASSWORD.' }
else { Write-Host 'WARN SIGOV_HML_ADMIN_PASSWORD não informado; gere senha temporária fora do repositório.' }
Write-Host 'URLs: API http://localhost:5001 | Web http://localhost:5000'
Write-Host 'Demo data habilitado:' $env:SIGOV_HML_ENABLE_DEMO_DATA
