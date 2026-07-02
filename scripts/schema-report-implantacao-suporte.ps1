param(
  [string]$ConnectionString = $env:SIGOV_DATABASE_URL,
  [string]$Output = "docs/schema-report-implantacao-suporte-local.md"
)
$ErrorActionPreference = "Stop"
$sql = Get-Content "database/diagnostics/schema-report-implantacao-suporte.sql" -Raw
if (-not $ConnectionString) {
  "# Schema report - implantação, suporte e POC`n`nExecução local não realizada: variável SIGOV_DATABASE_URL não informada. O sistema segue com fallback honesto via IDatabaseSchemaInspector." | Set-Content $Output -Encoding UTF8
  exit 0
}
$rows = psql $ConnectionString -c $sql --csv
"# Schema report - implantação, suporte e POC`n`n``````csv`n$($rows -join "`n")`n```````n" | Set-Content $Output -Encoding UTF8
