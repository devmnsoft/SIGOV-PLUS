param(
    [string]$ComposeService = "postgres",
    [string]$Database = "sigov",
    [string]$User = "sigov",
    [string]$Output = "docs/schema-report-local.md"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$sql = Join-Path $root "database/diagnostics/schema-report.sql"
$out = Join-Path $root $Output
$tmp = [System.IO.Path]::GetTempFileName()
try {
    Get-Content $sql -Raw | docker compose exec -T $ComposeService psql -U $User -d $Database -f - > $tmp
    $content = Get-Content $tmp -Raw
    @("# Relatório local do schema PostgreSQL", "", "Gerado em: $(Get-Date -Format o)", "", "```text", $content, "```") | Set-Content -Path $out -Encoding UTF8
    Write-Host "Relatório salvo em $out"
}
finally {
    Remove-Item $tmp -ErrorAction SilentlyContinue
}
