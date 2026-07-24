param(
  [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
  [string]$Output = "docs/schema-report-matriz-aderencia-local.md"
)
$ErrorActionPreference = "Stop"
function Sanitize-Message([string]$Message) {
  if ([string]::IsNullOrWhiteSpace($Message)) { return '' }
  return ($Message -replace '(?i)(password|pwd)\s*=\s*[^;\s]+','$1=***' -replace 'postgres(ql)?://[^\s]+','postgres://***')
}
$sql = Get-Content "database/diagnostics/schema-report-matriz-aderencia.sql" -Raw
$parent = Split-Path $Output -Parent
if (-not [string]::IsNullOrWhiteSpace($parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  $report = @(
    '# Schema report - matriz de aderência'
    ''
    'Status: not-executed'
    ''
    'Connection string não informada. Execute com SIGOV_CONNECTION_STRING ou -ConnectionString para gerar relatório real.'
    ''
    'Consulta preparada em database/diagnostics/schema-report-matriz-aderencia.sql.'
  )
  $report | Set-Content -Path $Output -Encoding UTF8
  Write-Warning "Connection string ausente; relatório marcado como not-executed em $Output"
  exit 0
}
$tmp = New-TemporaryFile
try {
  & psql $ConnectionString -c $sql --csv | Set-Content -Path $tmp -Encoding UTF8
  if ($LASTEXITCODE -ne 0) { throw "psql retornou código $LASTEXITCODE" }
  if (-not (Test-Path $tmp) -or ((Get-Item $tmp).Length -eq 0)) { throw 'psql não gerou resultado para o relatório.' }
  $csv = Get-Content $tmp -Raw
  $report = @(
    '# Schema report - matriz de aderência'
    ''
    'Status: executed'
    ''
    "Gerado em $(Get-Date -Format o)."
    ''
    '```csv'
    $csv
    '```'
  )
  $report | Set-Content -Path $Output -Encoding UTF8
  if (-not (Test-Path $Output) -or ((Get-Item $Output).Length -eq 0)) { throw 'Relatório não foi gerado.' }
} catch {
  throw (Sanitize-Message $_.Exception.Message)
} finally {
  Remove-Item -Path $tmp -Force -ErrorAction SilentlyContinue
}
