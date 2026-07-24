param(
  [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
  [string]$Output = "docs/schema-report-workflow-local.md"
)
$ErrorActionPreference = "Stop"
function Sanitize-Message([string]$Message) {
  if ([string]::IsNullOrWhiteSpace($Message)) { return '' }
  return ($Message -replace '(?i)(password|pwd)\s*=\s*[^;\s]+','$1=***' -replace 'postgres(ql)?://[^\s]+','postgres://***')
}
$sql = Get-Content "database/diagnostics/schema-report-workflow.sql" -Raw
$parent = Split-Path $Output -Parent
if (-not [string]::IsNullOrWhiteSpace($parent)) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
  $report = @(
    '# Schema report workflow local'
    ''
    'Status: not-executed'
    ''
    '> Connection string não informada. Defina SIGOV_CONNECTION_STRING para executar o diagnóstico real.'
  )
  $report | Set-Content -Path $Output -Encoding UTF8
  exit 0
}
try {
  $result = & psql $ConnectionString -c $sql --csv
  if ($LASTEXITCODE -ne 0) { throw "psql retornou código $LASTEXITCODE" }
  $report = @(
    '# Schema report workflow local'
    ''
    'Status: executed'
    ''
    "Executado em: $(Get-Date -Format o)"
    ''
    '```csv'
    $result
    '```'
  )
  $report | Set-Content -Path $Output -Encoding UTF8
} catch {
  throw (Sanitize-Message $_.Exception.Message)
}
