param(
    [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
    [string]$Output = "docs/schema-report-setorial-local.md"
)
$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { $ConnectionString = "Host=localhost;Port=5432;Database=sigov;Username=sigov;Password=sigov" }
$sql = Get-Content "database/diagnostics/schema-report-setorial.sql" -Raw
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ssK"
try {
    $rows = & psql $ConnectionString -c $sql --csv 2>$null
    @("# Schema report setorial local", "", "Gerado em: $timestamp", "", "````csv", $rows, "````") | Set-Content $Output -Encoding UTF8
} catch {
    @("# Schema report setorial local", "", "Gerado em: $timestamp", "", "Não foi possível consultar o PostgreSQL local. Fallback honesto: execute este script em ambiente com psql e banco disponível.", "", "Erro controlado: $($_.Exception.Message)") | Set-Content $Output -Encoding UTF8
}
