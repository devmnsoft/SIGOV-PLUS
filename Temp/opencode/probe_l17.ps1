$ErrorActionPreference='Stop'
$lines=[System.IO.File]::ReadAllLines('C:\MNSOFT\SIGOV-PLUS\src\Sigov.Infrastructure\ComprasEmpresariais\ComprasRepositories.cs')
$full=$lines[16]
$ix=$full.IndexOf('const string sql=')
Write-Output ('INSERT_SQL_TOTAL_LEN=' + $full.Length)
Write-Output $full.Substring($ix)
Write-Output '----STALE-RESPONDER-PROBE----'
$h=@{ Authorization='Bearer jornada-analista-rc5068a-2026-sigov-demo-token'; Host='municipio-demo.sigov.local'; 'Idempotency-Key'='jb-probe-stale-fx3' }
$body='{"conviteId":"136f2796-9a95-4496-8125-75948bb4cb3f","conviteVersion":999,"itens":[{"requisicaoItemId":"d0000001-0000-4000-8000-000000000101","precoUnitario":100,"desconto":0,"imposto":0,"frete":25,"prazoDias":10,"recusado":false}]}'
try { Invoke-WebRequest -Uri 'http://localhost:5001/api/compras-empresariais/cotacoes/c337b50c-f5b8-43f7-bbe2-e85e9c242561/respostas' -Method Post -Headers $h -Body $body -ContentType 'application/json' -UseBasicParsing | Out-Null }
catch {
  $resp=$_.Exception.Response
  $sr=New-Object System.IO.StreamReader($resp.GetResponseStream(),[System.Text.Encoding]::UTF8)
  Write-Output ([string]$resp.StatusCode)
  Write-Output ($sr.ReadToEnd())
}
