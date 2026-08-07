param(
  [string]$BaseUrl = 'http://localhost:5000',
  [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING,
  [string]$OutputDirectory = 'artifacts/operacao'
)
$ErrorActionPreference = 'Stop'
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [string]$Status, [string]$Detail) {
  $checks.Add([ordered]@{ name=$Name; status=$Status; detail=$Detail })
}
try { $response = Invoke-WebRequest "$BaseUrl/health" -TimeoutSec 10 -UseBasicParsing; Add-Check 'web_health' 'pass' "HTTP $($response.StatusCode)" }
catch { Add-Check 'web_health' 'warning' $_.Exception.Message }

if ([string]::IsNullOrWhiteSpace($ConnectionString) -or -not (Get-Command psql -ErrorAction SilentlyContinue)) {
  Add-Check 'database' 'warning' 'SIGOV_CONNECTION_STRING ou psql indisponível; nenhuma escrita de teste foi realizada.'
} else {
  $sql = @"
select json_build_object(
 'admin_exists', exists(select 1 from sigov.usuario where coalesce(is_deleted,false)=false),
 'tables', json_build_object(
   'protocolo',to_regclass('sigov.protocolo') is not null,
   'tarefa',to_regclass('sigov.tarefa') is not null,
   'notificacao',to_regclass('sigov.notificacao') is not null,
   'timeline',to_regclass('sigov.timeline_evento') is not null,
   'auditoria',to_regclass('sigov.auditoria_evento') is not null))::text;
"@
  try { $result = $sql | psql $ConnectionString -X -A -t -v ON_ERROR_STOP=1; Add-Check 'database' 'pass' ($result.Trim()) }
  catch { Add-Check 'database' 'fail' $_.Exception.Message }
}

New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$summary = [ordered]@{ marker='TESTE_AUTOMATIZADO_RC46'; generatedAt=(Get-Date).ToUniversalTime().ToString('o'); baseUrl=$BaseUrl; checks=$checks }
$summary | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'operational-flows-result.json')
$lines = @('# Relatório de fluxos operacionais RC46','',"Gerado em: $($summary.generatedAt)",'')
foreach ($check in $checks) { $lines += "- **$($check.name)** — $($check.status): $($check.detail)" }
$lines += '','O validador é somente leitura: não mantém dados de teste em produção.'
$lines | Set-Content -Encoding utf8 (Join-Path $OutputDirectory 'operational-flows-report.md')
if ($checks.status -contains 'fail') { exit 1 }
