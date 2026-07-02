param(
  [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
  [string]$Output = "docs/schema-report-workflow-local.md"
)
$ErrorActionPreference = "Stop"
$sql = Get-Content "database/diagnostics/schema-report-workflow.sql" -Raw
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  @("# Schema report workflow local", "", "> Connection string não informada. Defina SIGOV_CONNECTION_STRING para executar o diagnóstico real.") | Set-Content $Output -Encoding UTF8
  exit 0
}
$result = psql $ConnectionString -c $sql --csv
@("# Schema report workflow local", "", "Executado em: $(Get-Date -Format o)", "", "```csv") + $result + @("```") | Set-Content $Output -Encoding UTF8
