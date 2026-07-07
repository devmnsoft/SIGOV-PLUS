param(
  [string]$WebBaseUrl = "http://localhost:8080",
  [string]$ApiBaseUrl = "http://localhost:5001",
  [string]$OutputPath = "docs/smoke-test-release-candidate.md"
)

$ErrorActionPreference = "Continue"
$results = New-Object System.Collections.Generic.List[object]
function Add-Result([string]$Name,[string]$Url,[int]$Status,[bool]$Ok,[long]$Ms,[bool]$Blocking,[string]$Error) {
  $safeError = ($Error -replace [regex]::Escape($env:SIGOV_SMOKE_API_KEY), '[api-key-masked]')
  $results.Add([pscustomobject]@{ Name=$Name; Url=$Url; Status=$Status; Ok=$Ok; Ms=$Ms; Blocking=$Blocking; Error=$safeError })
}
function Invoke-SmokeRoute([string]$Name,[string]$Url,[int[]]$ExpectedStatus = @(200,302),[hashtable]$Headers = @{},[bool]$Blocking = $true) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  try {
    $response = Invoke-WebRequest $Url -Headers $Headers -UseBasicParsing -TimeoutSec 15 -MaximumRedirection 0 -ErrorAction Stop
    $status = [int]$response.StatusCode
    Add-Result $Name $Url $status ($ExpectedStatus -contains $status) $sw.ElapsedMilliseconds $Blocking ""
  } catch {
    $status = 0
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) { $status = [int]$_.Exception.Response.StatusCode }
    Add-Result $Name $Url $status ($ExpectedStatus -contains $status) $sw.ElapsedMilliseconds $Blocking $_.Exception.Message
  } finally { $sw.Stop() }
}

$webRoutes = @('/','/Auth/Login','/Dashboard','/MinhaCentral','/Protocolo','/Protocolo/Novo','/Ged','/Ged/NovoDocumento','/Workflow','/Tarefas','/Notificacoes','/Busca?q=protocolo','/Relatorios','/Poc','/Seguranca/ApiKeys','/Integracoes/Webhooks','/ValidarDocumento','/Operacao/Outbox')
foreach ($route in $webRoutes) { Invoke-SmokeRoute "WEB $route" "$WebBaseUrl$route" @(200,302) @{} $true }
foreach ($route in @('/api/health/live','/api/health/ready','/api/health/db','/api/v1/health')) { Invoke-SmokeRoute "API $route" "$ApiBaseUrl$route" @(200) @{} $true }
Invoke-SmokeRoute 'API /api/v1/protocolos sem API key' "$ApiBaseUrl/api/v1/protocolos" @(401) @{} $true

if ($env:SIGOV_SMOKE_API_KEY -and $env:SIGOV_SMOKE_TENANT_ID) {
  $headers = @{ 'X-Api-Key'=$env:SIGOV_SMOKE_API_KEY; 'X-Tenant-Id'=$env:SIGOV_SMOKE_TENANT_ID }
  Invoke-SmokeRoute 'API /api/v1/protocolos com API key válida' "$ApiBaseUrl/api/v1/protocolos" @(200) $headers $true
  Invoke-SmokeRoute 'API /api/v1/documentos com API key válida' "$ApiBaseUrl/api/v1/documentos" @(200) $headers $true
  Invoke-SmokeRoute 'API /api/v1/tarefas com API key válida' "$ApiBaseUrl/api/v1/tarefas" @(200) $headers $true
} else {
  Add-Result 'API /api/v1/protocolos com API key válida' "$ApiBaseUrl/api/v1/protocolos" 0 $true 0 $false 'Não executado: defina SIGOV_SMOKE_API_KEY e SIGOV_SMOKE_TENANT_ID.'
  Add-Result 'API /api/v1/documentos com API key válida' "$ApiBaseUrl/api/v1/documentos" 0 $true 0 $false 'Não executado: defina SIGOV_SMOKE_API_KEY e SIGOV_SMOKE_TENANT_ID.'
  Add-Result 'API /api/v1/tarefas com API key válida' "$ApiBaseUrl/api/v1/tarefas" 0 $true 0 $false 'Não executado: defina SIGOV_SMOKE_API_KEY e SIGOV_SMOKE_TENANT_ID.'
}

$total = $results.Count; $success = @($results | Where-Object Ok).Count; $failedBlocking = @($results | Where-Object { -not $_.Ok -and $_.Blocking }).Count
$generatedAt = Get-Date -Format o
$lines = @('# Smoke test Release Candidate SIGOV PLUS','',"Gerado em $generatedAt.",'',"Resumo: $success/$total checks OK; $failedBlocking falhas bloqueantes.",'','Critérios: rotas Web/health esperam 200/302, API v1 sem chave espera 401, API v1 com chave espera 200 quando credenciais de smoke forem fornecidas.','', '| Check | URL | Status | OK | Bloqueante | ms | Erro |','|---|---|---:|---|---|---:|---|')
foreach ($item in $results) { $err = ($item.Error -replace '\|','/' -replace "`r?`n", ' '); $lines += "| $($item.Name) | $($item.Url) | $($item.Status) | $($item.Ok) | $($item.Blocking) | $($item.Ms) | $err |" }
$dir = Split-Path $OutputPath -Parent; if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$lines | Out-File -FilePath $OutputPath -Encoding utf8
$results | ConvertTo-Json -Depth 5 | Out-File -FilePath ($OutputPath -replace '\.md$', '.json') -Encoding utf8
Write-Host "Smoke test SIGOV PLUS: $success/$total OK; $failedBlocking falhas bloqueantes. Resultado: $OutputPath"
if ($failedBlocking -gt 0) { exit 1 }
