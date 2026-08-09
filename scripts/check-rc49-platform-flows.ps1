param([string]$BaseUrl = 'http://localhost:5000', [string]$ConnectionString = $env:SIGOV_CONNECTION_STRING, [string]$OutputDirectory = 'artifacts/rc49')
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root $OutputDirectory
New-Item -ItemType Directory -Force -Path $out | Out-Null
$checks = [System.Collections.Generic.List[object]]::new()
function Add-Check([string]$Name, [bool]$Passed, [string]$Evidence) { $checks.Add([pscustomobject]@{ name=$Name; passed=$Passed; evidence=$Evidence; checkedAt=(Get-Date).ToUniversalTime().ToString('o') }) }
$required = @('sigov.workflow_definicao','sigov.workflow_versao','sigov.workflow_etapa','sigov.workflow_transicao','sigov.workflow_evento')
$manifest = Get-Content (Join-Path $root 'database/postgres/migrations/manifest.json') -Raw | ConvertFrom-Json
Add-Check 'migration-manifest' (($manifest.migrations.file -contains '20260809160000_rc49_workflow_platform.sql')) 'RC49 registrada no manifest'
$sql = Get-Content (Join-Path $root 'database/postgres/migrations/20260809160000_rc49_workflow_platform.sql') -Raw
foreach($table in $required){ Add-Check "schema-$table" ($sql -match [regex]::Escape("create table if not exists $table")) 'DDL idempotente presente' }
$permissions = @('WORKFLOW_CONSULTA','WORKFLOW_GERENCIAR','FORMULARIO_CONSULTA','PORTAL_CONFIGURAR','SLA_CONSULTA','APROVACAO_DECIDIR','TEMPLATE_GERENCIAR','RELATORIO_EXECUTIVO')
foreach($permission in $permissions){ Add-Check "permission-$permission" ($sql.Contains($permission)) 'Permissão versionada' }
$routes = @('/Workflows','/Workflows/Novo','/Workflows/Designer/1','/health')
if($ConnectionString -and (Get-Command psql -ErrorAction SilentlyContinue)){ $tables = & psql $ConnectionString -Atc "select count(*) from unnest(array['workflow_definicao','workflow_versao','workflow_etapa','workflow_transicao','workflow_evento']) t where to_regclass('sigov.'||t) is not null"; Add-Check 'database-live' ($tables -eq '5') "$tables/5 tabelas" } else { Add-Check 'database-live' $false 'Não executado: SIGOV_CONNECTION_STRING/psql indisponível' }
foreach($route in $routes){ try { $response=Invoke-WebRequest -Uri ($BaseUrl+$route) -MaximumRedirection 0 -SkipHttpErrorCheck -TimeoutSec 5; Add-Check "route-$route" ($response.StatusCode -in 200,302,401,403) "HTTP $($response.StatusCode)" } catch { Add-Check "route-$route" $false 'Runtime indisponível' } }
$secretPattern='(?i)(password|senha|token)\s*[=:]\s*[^*\s]{6,}'
$trackedLogs = Get-ChildItem (Join-Path $root 'logs') -File -ErrorAction SilentlyContinue
$leaks = @($trackedLogs | Select-String -Pattern $secretPattern)
Add-Check 'logs-without-secrets' ($leaks.Count -eq 0) "$($leaks.Count) ocorrência(s)"
$result=[pscustomobject]@{release='RC49'; generatedAt=(Get-Date).ToUniversalTime().ToString('o'); passed=(@($checks|Where-Object passed).Count); failed=(@($checks|Where-Object {-not $_.passed}).Count); checks=$checks}
$result|ConvertTo-Json -Depth 5|Set-Content (Join-Path $out 'platform-flows-result.json') -Encoding utf8
$lines=@('# SIGOV+ RC49 — validação de fluxos','',"Gerado em: $($result.generatedAt)",'',"Aprovados: **$($result.passed)** · Pendentes: **$($result.failed)**",'') + @($checks|ForEach-Object { "- $(if($_.passed){'[x]'}else{'[ ]'}) $($_.name): $($_.evidence)" })
$lines|Set-Content (Join-Path $out 'platform-flows-report.md') -Encoding utf8
$checks|ForEach-Object { "[$($_.checkedAt)] $($_.name) passed=$($_.passed) evidence=$($_.evidence)" }|Set-Content (Join-Path $out 'platform-flows.log') -Encoding utf8
if($result.failed -gt 0){ Write-Warning "$($result.failed) validações dependem do runtime/banco." }; Write-Host "Relatório RC49: $out"
