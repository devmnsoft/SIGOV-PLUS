$ErrorActionPreference = "Continue"
$out = Join-Path $PSScriptRoot "../docs/smoke-test-api-mobile-integracoes.md"
$urls = @(
 "http://localhost:5001/api/health/live", "http://localhost:5001/swagger", "http://localhost:8080/Seguranca/ApiKeys",
 "http://localhost:8080/Integracoes/Webhooks", "http://localhost:8080/MobileCampo/Sincronizacao", "http://localhost:8080/AssinaturasDigitais",
 "http://localhost:8080/ValidarDocumento", "http://localhost:8080/Bi/Fluxos", "http://localhost:8080/Operacao/ApiLogs", "http://localhost:8080/Operacao/Outbox"
)
"# Smoke test — API, Mobile e Integrações`n`nGerado em: $(Get-Date -Format o)`n" | Set-Content $out -Encoding UTF8
foreach ($url in $urls) {
  try { $r = Invoke-WebRequest $url -UseBasicParsing -TimeoutSec 15; "- PASS $url => $($r.StatusCode)" | Add-Content $out -Encoding UTF8 }
  catch { "- FAIL $url => $($_.Exception.Message)" | Add-Content $out -Encoding UTF8 }
}
