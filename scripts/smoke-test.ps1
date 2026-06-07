param(
  [string]$ApiBaseUrl = 'http://localhost:5001',
  [string]$WebBaseUrl = 'http://localhost:5000',
  [string]$Username = $env:SIGOV_SMOKE_USERNAME,
  [string]$Password = $env:SIGOV_SMOKE_PASSWORD,
  [string]$Tenant = $env:SIGOV_SMOKE_TENANT,
  [switch]$SkipLogin,
  [string]$Environment = $env:ASPNETCORE_ENVIRONMENT
)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($Environment)) { $Environment = 'Development' }
$headers = @{}
if (-not [string]::IsNullOrWhiteSpace($Tenant)) { $headers['X-Sigov-Tenant'] = $Tenant }
$required = @('/api/health','/api/health/live','/api/health/ready','/api/health/db','/api/health/outbox','/api/health/version')
$optional = @('/api/tenant/atual','/api/saas/tenant/atual','/api/modulos-contratados','/api/pessoas','/api/auditoria','/api/suporte/chamados','/api/integracoes/outbox')
function Test-Endpoint([string]$Url, [bool]$Required) {
  try {
    $response = Invoke-WebRequest -Uri $Url -Headers $headers -UseBasicParsing -TimeoutSec 15
    if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) { Write-Host "PASS $Url"; return }
    throw "HTTP $($response.StatusCode)"
  } catch {
    if ($Required) { throw "Smoke obrigatório falhou em ${Url}: $($_.Exception.Message)" }
    Write-Host "WARN endpoint opcional indisponível: $Url"
  }
}
foreach ($path in $required) { Test-Endpoint "$ApiBaseUrl$path" $true }
if ($Environment -eq 'Development') { Test-Endpoint "$ApiBaseUrl/swagger" $false }
Test-Endpoint $WebBaseUrl $true
foreach ($path in $optional) { Test-Endpoint "$ApiBaseUrl$path" $false }
if (-not $SkipLogin) {
  if ([string]::IsNullOrWhiteSpace($Password)) { Write-Host 'WARN login ignorado: SIGOV_SMOKE_PASSWORD não informado.' }
  else { Write-Host "INFO login smoke configurado para $Username com credencial fornecida por variável segura." }
}
Write-Host 'Smoke tests concluídos.'
