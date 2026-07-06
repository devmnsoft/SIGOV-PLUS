param(
  [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
  [string]$Output = "docs/schema-report-matriz-aderencia-local.md"
)
$ErrorActionPreference = "Stop"
$sql = Get-Content "database/diagnostics/schema-report-matriz-aderencia.sql" -Raw
New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  @("# Schema report - matriz de aderência", "", "Connection string não informada. Execute com SIGOV_CONNECTION_STRING ou -ConnectionString.", "", "Consulta preparada em database/diagnostics/schema-report-matriz-aderencia.sql.") | Set-Content $Output -Encoding UTF8
  Write-Warning "Connection string ausente; relatório fallback criado em $Output"
  exit 0
}
$tmp = New-TemporaryFile
psql $ConnectionString -c $sql --csv | Set-Content $tmp -Encoding UTF8
@("# Schema report - matriz de aderência", "", "Gerado em $(Get-Date -Format o).", "", "```csv", (Get-Content $tmp -Raw), "```") | Set-Content $Output -Encoding UTF8
