param(
  [string]$WebBaseUrl = "http://localhost:8080",
  [string]$ApiBaseUrl = "http://localhost:5001",
  [string]$OutputPath = "docs/runtime-smoke-tests.md"
)
$ErrorActionPreference = "Continue"
$routes = @(
  "$WebBaseUrl/Auth/Login", "$WebBaseUrl/Dashboard", "$WebBaseUrl/MinhaCentral", "$WebBaseUrl/Pessoas", "$WebBaseUrl/Rh",
  "$WebBaseUrl/Protocolo", "$WebBaseUrl/Ged", "$WebBaseUrl/Workflow", "$WebBaseUrl/Tarefas", "$WebBaseUrl/Notificacoes",
  "$WebBaseUrl/Compras", "$WebBaseUrl/Licitacoes", "$WebBaseUrl/Contratos", "$WebBaseUrl/Siafic", "$WebBaseUrl/Patrimonio",
  "$WebBaseUrl/Obras", "$WebBaseUrl/Portal", "$WebBaseUrl/Ouvidoria", "$WebBaseUrl/Suporte", "$WebBaseUrl/POC",
  "$WebBaseUrl/Busca?q=teste", "$WebBaseUrl/Relatorios", "$WebBaseUrl/Operacao/Health", "$ApiBaseUrl/api/health/live"
)
$results = @()
foreach ($url in $routes) {
  $sw = [Diagnostics.Stopwatch]::StartNew()
  try {
    $r = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 15 -MaximumRedirection 5
    $results += [pscustomobject]@{ Url=$url; Status=[int]$r.StatusCode; Ok=($r.StatusCode -ge 200 -and $r.StatusCode -lt 400); Ms=$sw.ElapsedMilliseconds; Error="" }
  } catch {
    $status = 0
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) { $status = [int]$_.Exception.Response.StatusCode }
    $results += [pscustomobject]@{ Url=$url; Status=$status; Ok=$false; Ms=$sw.ElapsedMilliseconds; Error=$_.Exception.Message }
  }
}
$lines = @("# Runtime smoke tests SIGOV PLUS", "", "Gerado em $(Get-Date -Format o).", "", "| Rota | Status | OK | ms | Erro |", "|---|---:|---|---:|---|")
foreach ($x in $results) { $lines += "| $($x.Url) | $($x.Status) | $($x.Ok) | $($x.Ms) | $($x.Error -replace '\|','/') |" }
$lines | Out-File -FilePath $OutputPath -Encoding utf8
if ($results.Where({ -not $_.Ok }).Count -gt 0) { exit 1 }
