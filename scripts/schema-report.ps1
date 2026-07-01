param(
    [string]$Container = "sigov-postgres",
    [string]$Database = $env:POSTGRES_DB,
    [string]$User = $env:POSTGRES_USER
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$sqlPath = Join-Path $repoRoot "database/diagnostics/schema-report.sql"
$outPath = Join-Path $repoRoot "docs/schema-report-local.md"

if ([string]::IsNullOrWhiteSpace($Database)) { $Database = "sigov" }
if ([string]::IsNullOrWhiteSpace($User)) { $User = "sigov" }
if (-not (Test-Path $sqlPath)) { throw "SQL não encontrado: $sqlPath" }

$sql = Get-Content $sqlPath -Raw
$tmp = "/tmp/schema-report.sql"
$sql | docker exec -i $Container sh -lc "cat > $tmp"
$result = docker exec -i $Container psql -U $User -d $Database -f $tmp --pset border=2 --pset pager=off

@("# Schema report local", "", "Gerado em: $(Get-Date -Format o)", "Container: $Container", "Database: $Database", "User: $User", "", '```text', $result, '```') | Set-Content -Path $outPath -Encoding UTF8
Write-Host "Relatório salvo em $outPath"
