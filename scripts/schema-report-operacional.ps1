param(
  [string]$Container = "sigov-plus-postgres-1",
  [string]$Database = "sigov",
  [string]$User = "sigov",
  [string]$Output = "docs/schema-report-operacional-local.md"
)
$ErrorActionPreference = "Stop"
$sql = Get-Content "database/diagnostics/schema-report-operacional.sql" -Raw
$rows = docker exec -i $Container psql -U $User -d $Database -A -F '|' -c $sql
@("# Schema report operacional local", "", "Gerado em: $(Get-Date -Format o)", "", '```text', $rows, '```') | Set-Content $Output -Encoding UTF8
Write-Host "Relatório salvo em $Output"
