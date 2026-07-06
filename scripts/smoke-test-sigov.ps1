param(
  [string]$WebBaseUrl = "http://localhost:8080",
  [string]$ApiBaseUrl = "http://localhost:5001",
  [string]$OutputPath = "docs/smoke-test-release-candidate.md"
)

$ErrorActionPreference = "Continue"
$routes = @(
  "$WebBaseUrl/Auth/Login",
  "$WebBaseUrl/Dashboard",
  "$WebBaseUrl/MinhaCentral",
  "$WebBaseUrl/Pessoas",
  "$WebBaseUrl/Rh",
  "$WebBaseUrl/Protocolo",
  "$WebBaseUrl/Ged",
  "$WebBaseUrl/Workflow",
  "$WebBaseUrl/Tarefas",
  "$WebBaseUrl/Notificacoes",
  "$WebBaseUrl/Agenda",
  "$WebBaseUrl/Compras",
  "$WebBaseUrl/Licitacoes",
  "$WebBaseUrl/Contratos",
  "$WebBaseUrl/Siafic",
  "$WebBaseUrl/Patrimonio",
  "$WebBaseUrl/Obras",
  "$WebBaseUrl/PortalCidadao",
  "$WebBaseUrl/Ouvidoria",
  "$WebBaseUrl/Busca?q=teste",
  "$WebBaseUrl/Relatorios",
  "$WebBaseUrl/Poc",
  "$WebBaseUrl/Operacao/Health",
  "$ApiBaseUrl/api/health/live",
  "$ApiBaseUrl/api/health/ready",
  "$ApiBaseUrl/api/health/db"
)

$results = @()
foreach ($url in $routes) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  try {
    $response = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 15 -MaximumRedirection 0 -ErrorAction Stop
    $status = [int]$response.StatusCode
    $ok = ($status -eq 200 -or $status -eq 302)
    $results += [pscustomobject]@{ Url=$url; Status=$status; Ok=$ok; Ms=$sw.ElapsedMilliseconds; Error="" }
  } catch {
    $status = 0
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
      $status = [int]$_.Exception.Response.StatusCode
    }
    $ok = ($status -eq 200 -or $status -eq 302)
    $results += [pscustomobject]@{ Url=$url; Status=$status; Ok=$ok; Ms=$sw.ElapsedMilliseconds; Error=$_.Exception.Message }
  } finally {
    $sw.Stop()
  }
}

$total = $results.Count
$success = @($results | Where-Object { $_.Ok }).Count
$failed = $total - $success
$generatedAt = Get-Date -Format o
$lines = @(
  "# Smoke test Release Candidate SIGOV PLUS",
  "",
  "Gerado em $generatedAt.",
  "",
  "Resumo: $success/$total rotas OK; $failed falhas.",
  "",
  "Critério de OK: HTTP 200 ou 302 esperado. O script não interrompe na primeira falha.",
  "",
  "| Rota | Status | OK | ms | Erro |",
  "|---|---:|---|---:|---|"
)
foreach ($item in $results) {
  $errorText = ($item.Error -replace '\|','/' -replace "`r?`n", ' ')
  $lines += "| $($item.Url) | $($item.Status) | $($item.Ok) | $($item.Ms) | $errorText |"
}
$lines | Out-File -FilePath $OutputPath -Encoding utf8
$results | ConvertTo-Json -Depth 3 | Out-File -FilePath ($OutputPath -replace '\.md$', '.json') -Encoding utf8

Write-Host "Smoke test SIGOV PLUS: $success/$total OK; $failed falhas. Resultado: $OutputPath"
if ($failed -gt 0) { exit 1 }
