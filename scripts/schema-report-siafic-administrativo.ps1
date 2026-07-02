param(
  [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
  [string]$Output = "docs/schema-report-siafic-administrativo-local.md"
)
$ErrorActionPreference = "Stop"
$sql = Get-Content "database/diagnostics/schema-report-siafic-administrativo.sql" -Raw
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  @("# Schema report SIAFIC administrativo local", "", "Conexão não informada. Defina SIGOV_CONNECTION_STRING para executar o diagnóstico real.") | Set-Content $Output -Encoding UTF8
  exit 0
}
$tmp = New-TemporaryFile
psql $ConnectionString -c $sql --csv | Set-Content $tmp -Encoding UTF8
@("# Schema report SIAFIC administrativo local", "", "Gerado em $(Get-Date -Format o).", "", '```csv', (Get-Content $tmp -Raw), '```') | Set-Content $Output -Encoding UTF8
