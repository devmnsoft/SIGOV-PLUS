param(
  [string]$ConnectionString = $env:SIGOV_DATABASE_URL,
  [string]$Output = "docs/schema-report-patrimonio-obras-local.md"
)
$ErrorActionPreference = "Stop"
$sql = Get-Content "database/diagnostics/schema-report-patrimonio-obras.sql" -Raw
New-Item -ItemType Directory -Force -Path (Split-Path $Output) | Out-Null
"# Schema report — Patrimônio, Inventário e Obras`n`nGerado em: $(Get-Date -Format o)`n" | Set-Content $Output -Encoding UTF8
if (-not $ConnectionString) {
  "Status: Em implantação neste ambiente — informe SIGOV_DATABASE_URL para consultar o PostgreSQL real.`n" | Add-Content $Output -Encoding UTF8
  exit 0
}
if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
  "Status: Em implantação neste ambiente — psql não disponível no runner.`n" | Add-Content $Output -Encoding UTF8
  exit 0
}
"``````sql`n$sql`n```````n" | Add-Content $Output -Encoding UTF8
$rows = & psql $ConnectionString -P format=aligned -P border=2 -c $sql
"``````text`n$rows`n```````n" | Add-Content $Output -Encoding UTF8
