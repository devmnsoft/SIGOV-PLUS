param(
  [string]$ConnectionString = $env:SIGOV_DATABASE_URL,
  [string]$OutputPath = "docs/schema-report-consolidacao-modulos-local.md"
)
$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  $ConnectionString = "host=localhost port=5432 dbname=sigov user=sigov password=sigov"
}
$sql = "database/diagnostics/schema-report-consolidacao-modulos.sql"
$tmp = New-TemporaryFile
try {
  psql $ConnectionString -f $sql | Out-File -FilePath $tmp -Encoding utf8
  "# Schema report local - consolidação de módulos`n`nGerado em $(Get-Date -Format o).`n`n``````text" | Out-File -FilePath $OutputPath -Encoding utf8
  Get-Content $tmp | Out-File -FilePath $OutputPath -Encoding utf8 -Append
  "``````" | Out-File -FilePath $OutputPath -Encoding utf8 -Append
  Write-Host "Schema report salvo em $OutputPath"
} finally { Remove-Item $tmp -ErrorAction SilentlyContinue }
