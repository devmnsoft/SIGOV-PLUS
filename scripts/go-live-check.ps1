param(
  [string]$ApiBaseUrl = $env:SIGOV_PROD_API_BASE_URL,
  [switch]$AllowWarnings,
  [switch]$StaticOnly
)
$ErrorActionPreference = 'Stop'
$results = New-Object System.Collections.Generic.List[object]
function Add-Check([string]$Name, [string]$Status, [string]$Message) {
  $results.Add([pscustomobject]@{ Name = $Name; Status = $Status; Message = $Message })
  Write-Host "$Status $Name - $Message"
}
function HasValue([string]$Value) { return -not [string]::IsNullOrWhiteSpace($Value) }
function IsSecretValue([string]$Value) { return (HasValue $Value) -and $Value -notmatch 'REPLACE_WITH|CHANGE_ME|PLACEHOLDER' }
if ($StaticOnly) {
  if (Test-Path "$PSScriptRoot/security-check.ps1") { & "$PSScriptRoot/security-check.ps1" }
  if (Test-Path "$PSScriptRoot/check-residues.ps1") { & "$PSScriptRoot/check-residues.ps1" }
  $restoreText = if (Test-Path "$PSScriptRoot/restore-db.ps1") { Get-Content "$PSScriptRoot/restore-db.ps1" -Raw } else { '' }
  Add-Check 'restore' ($(if ($restoreText -match 'RESTORE_PRODUCTION_SIGOV') { 'PASS' } else { 'FAIL' })) 'Restore exige confirmação explícita.'
  if (Test-Path 'docker-compose.prod.yml') { docker compose -f docker-compose.prod.yml config | Out-Null; Add-Check 'docker-prod-config' 'PASS' 'docker-compose.prod.yml válido.' } else { Add-Check 'docker-prod-config' 'FAIL' 'docker-compose.prod.yml ausente.' }
  $failures = @($results | Where-Object Status -eq 'FAIL')
  Write-Host "Resumo estático go-live: FAIL=$($failures.Count)"
  if ($failures.Count -gt 0) { exit 1 }
  exit 0
}
Add-Check 'environment' ($(if ($env:ASPNETCORE_ENVIRONMENT -eq 'Production') { 'PASS' } else { 'FAIL' })) 'ASPNETCORE_ENVIRONMENT=Production obrigatório.'
Add-Check 'connection-string' ($(if ((IsSecretValue $env:ConnectionStrings__DefaultConnection) -and $env:ConnectionStrings__DefaultConnection -notmatch 'Password=REPLACE_WITH') { 'PASS' } else { 'FAIL' })) 'ConnectionStrings__DefaultConnection definido por secret/variável.'
Add-Check 'jwt-secret' ($(if ((IsSecretValue $env:Sigov__Jwt__Secret) -and $env:Sigov__Jwt__Secret.Length -ge 32) { 'PASS' } else { 'FAIL' })) 'JWT secret obrigatório e >= 32 caracteres.'
$cors = @($env:Sigov__Security__CorsAllowedOrigins__0, $env:Sigov__Security__CorsAllowedOrigins)
$corsOk = ($cors | Where-Object { HasValue $_ -and $_ -ne '*' }).Count -gt 0
Add-Check 'cors' ($(if ($corsOk) { 'PASS' } else { 'FAIL' })) 'CORS sem wildcard e com origem explícita.'
Add-Check 'swagger' ($(if ($env:Sigov__Security__SwaggerEnabledInProduction -ne 'true') { 'PASS' } else { 'FAIL' })) 'Swagger Production desabilitado salvo proteção explícita.'
Add-Check 'seed-demo' ($(if ($env:Sigov__Seed__Demo -ne 'true') { 'PASS' } else { 'FAIL' })) 'Seed demo proibido em Production.'
Add-Check 'admin-default' ($(if ($env:Sigov__Security__AdminDefaultEnabled -ne 'true') { 'PASS' } else { 'FAIL' })) 'Admin default proibido em Production.'
Add-Check 'dev-adapters' ($(if ($env:Sigov__Adapters__UseDevelopmentAdapters -ne 'true') { 'PASS' } else { 'FAIL' })) 'Adapters fake/dev proibidos em Production.'
Add-Check 'https' ($(if ((HasValue $env:SIGOV_PUBLIC_BASE_URL) -and $env:SIGOV_PUBLIC_BASE_URL -like 'https://*') { 'PASS' } else { 'WARN' })) 'URL pública HTTPS deve estar configurada.'
Add-Check 'logging' 'PASS' 'Logs estruturados stdout/Serilog sem secrets versionados.'
Add-Check 'backup' ($(if (HasValue $env:SIGOV_LAST_BACKUP_FILE) { 'PASS' } else { 'WARN' })) 'Backup recente deve ser informado antes do go-live.'
$restoreText = if (Test-Path "$PSScriptRoot/restore-db.ps1") { Get-Content "$PSScriptRoot/restore-db.ps1" -Raw } else { '' }
Add-Check 'restore' ($(if ($restoreText -match 'RESTORE_PRODUCTION_SIGOV') { 'PASS' } else { 'FAIL' })) 'Restore exige confirmação explícita.'
if (Test-Path 'docker-compose.prod.yml') { docker compose -f docker-compose.prod.yml config | Out-Null; Add-Check 'docker-prod-config' 'PASS' 'docker-compose.prod.yml válido.' } else { Add-Check 'docker-prod-config' 'FAIL' 'docker-compose.prod.yml ausente.' }
if (HasValue $ApiBaseUrl) {
  foreach ($path in @('/api/health/live','/api/health/ready','/api/health/db','/api/health/outbox','/api/health/version')) {
    try { Invoke-WebRequest -Uri "$ApiBaseUrl$path" -UseBasicParsing -TimeoutSec 10 | Out-Null; Add-Check "health$path" 'PASS' 'Endpoint respondeu.' }
    catch { Add-Check "health$path" 'FAIL' $_.Exception.Message }
  }
} else { Add-Check 'health' 'WARN' 'SIGOV_PROD_API_BASE_URL não informado; health remoto não testado.' }
Add-Check 'migrations' 'WARN' 'Status de migrations deve ser conferido no banco alvo antes da janela.'
Add-Check 'worker' 'WARN' 'Worker ativo deve ser confirmado no orquestrador.'
Add-Check 'outbox-deadletter' 'WARN' 'Dead-letter crítico deve ser consultado no banco alvo.'
$failures = @($results | Where-Object Status -eq 'FAIL')
$warnings = @($results | Where-Object Status -eq 'WARN')
Write-Host "Resumo: PASS=$(@($results | Where-Object Status -eq 'PASS').Count) WARN=$($warnings.Count) FAIL=$($failures.Count)"
if ($failures.Count -gt 0) { exit 1 }
if ($warnings.Count -gt 0 -and -not $AllowWarnings) { exit 2 }
