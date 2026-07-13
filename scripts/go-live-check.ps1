param(
  [string]$PackagePath = 'artifacts/release/sigov-plus-1.0.0-rc-final',
  [switch]$AllowWarnings,
  [switch]$StaticOnly,
  [string]$ReleaseCandidateVersion = '1.0.0-rc-final'
)
$ErrorActionPreference='Stop'
$results=New-Object System.Collections.Generic.List[object]
function Add-Check([string]$Name,[string]$Status,[string]$Message){$results.Add([pscustomobject]@{name=$Name;status=$Status;message=$Message});Write-Host "$Status $Name - $Message"}
function Check-Path([string]$Name,[string]$Path,[bool]$Required=$true){ if(Test-Path $Path){Add-Check $Name 'PASS' "$Path encontrado."} elseif($Required){Add-Check $Name 'FAIL' "$Path ausente."} else {Add-Check $Name 'WARN' "$Path ainda não gerado."}}
$docs=@('README.md','docs/diagnostico-consolidacao-pos-rc-13.md','docs/evidencias-consolidacao-pos-rc-13.md','docs/manual-usuario-sigov-pos-rc-13.md','docs/manual-admin-sigov-pos-rc-13.md','docs/jornadas-operacionais-pos-rc-13.md','docs/matriz-funcional-pos-rc-13.md','docs/matriz-crud-enterprise-pos-rc-13.md','docs/security-lgpd-pos-rc-13.md','docs/checklist-homologacao-pos-rc-13.md','docs/importacao-enterprise-pos-rc-13.md','docs/acoes-lote-enterprise-pos-rc-13.md','docs/release-notes-v1.0.0.md','docs/roteiro-demo-sigov-plus.md','docs/checklist-go-live-pos-rc.md')
foreach($d in $docs){Check-Path "doc:$d" $d}

if(Test-Path 'README.md'){ $readme=Get-Content 'README.md' -Raw; if($readme -match 'SIGOV' -and ($readme -match 'Pós-RC' -or $readme -match 'Release')){Add-Check 'readme-release' 'PASS' 'README contém contexto SIGOV/release.'}else{Add-Check 'readme-release' 'WARN' 'README deve mencionar explicitamente Pós-RC/release.'} }
if(Test-Path '.env'){Add-Check 'env-real-ausente' 'FAIL' '.env real versionado/presente no workspace.'}else{Add-Check 'env-real-ausente' 'PASS' '.env real ausente.'}
foreach($required in @('scripts/smoke-test-sigov.ps1','scripts/schema-report.ps1','scripts/package-release.ps1')){Check-Path "script:$required" $required}
Check-Path 'smoke-md' 'docs/smoke-test-release-candidate.md' $false
Check-Path 'smoke-json' 'docs/smoke-test-release-candidate.json' $false
Check-Path 'ci-yml' '.github/workflows/ci.yml'
Check-Path 'release-yml' '.github/workflows/release.yml'
Check-Path 'docker-compose' 'docker-compose.yml'
Check-Path 'seed-demo' 'database/postgres/seeds/pos_rc_homologacao_demo.sql'
$migrations=@(Get-ChildItem database/postgres/migrations -Filter '*pos_rc*.sql' -ErrorAction SilentlyContinue); if($migrations.Count -gt 0){Add-Check 'migration-pos-rc' 'PASS' "$($migrations.Count) migration(s) Pós-RC."}else{Add-Check 'migration-pos-rc' 'FAIL' 'Migration Pós-RC ausente.'}
if(Test-Path '.env.example'){ $envEx=Get-Content '.env.example' -Raw; if($envEx -match 'POSTGRES_PASSWORD=123456'){Add-Check 'env-example-sanitized' 'FAIL' '.env.example contém senha trivial.'}elseif($envEx -match 'POSTGRES_DB=sigov' -and $envEx -match 'POSTGRES_USER=sigov' -and $envEx -match 'POSTGRES_PASSWORD=change_me_local_only'){Add-Check 'env-example-sanitized' 'PASS' '.env.example padronizado e sanitizado.'}else{Add-Check 'env-example-sanitized' 'FAIL' '.env.example fora do padrão sigov.'} } else { Add-Check 'env-example-sanitized' 'FAIL' '.env.example ausente.' }
if(Test-Path $PackagePath){
  Check-Path 'release-manifest' (Join-Path $PackagePath 'release-manifest.json')
  if(Test-Path (Join-Path $PackagePath '.env')){Add-Check 'package-env-real' 'FAIL' '.env real presente no pacote.'}else{Add-Check 'package-env-real' 'PASS' '.env real ausente.'}
  if(Test-Path (Join-Path $PackagePath 'storage')){Add-Check 'package-storage' 'FAIL' 'storage presente no pacote.'}else{Add-Check 'package-storage' 'PASS' 'storage ausente.'}
  $bad=Get-ChildItem $PackagePath -Recurse -File|Where-Object{$_.Name -match '(?i)\.pfx$|\.pem$|\.key$|\.bak$'}; if($bad){Add-Check 'package-secret-files' 'FAIL' "Arquivos bloqueados: $($bad.Name -join ', ')"}else{Add-Check 'package-secret-files' 'PASS' 'Sem .pfx/.pem/.key/.bak.'}
  $secret=Get-ChildItem $PackagePath -Recurse -File|Select-String -Pattern 'POSTGRES_PASSWORD=123456' -Quiet; if($secret){Add-Check 'package-secrets' 'FAIL' 'Secret/token ou exemplo inseguro detectado.'}else{Add-Check 'package-secrets' 'PASS' 'Scanner básico sem achados.'}
}else{Add-Check 'release-package' 'WARN' "Pacote $PackagePath ainda não gerado."}
$passed=@($results|Where-Object status -eq 'PASS').Count; $warnings=@($results|Where-Object status -eq 'WARN').Count; $failed=@($results|Where-Object status -eq 'FAIL').Count; $statusFinal=if($failed -gt 0){'BLOQUEADO'}elseif($warnings -gt 0){'APROVADO_COM_RESSALVAS'}else{'APROVADO'}; $summary=[ordered]@{generatedAt=(Get-Date).ToUniversalTime().ToString('o');total=$results.Count;passed=$passed;warnings=$warnings;failedBlocking=$failed;failedNonBlocking=0;statusFinal=$statusFinal;releaseCandidateVersion=$ReleaseCandidateVersion;results=$results}
$md=@('# Go-live check Pós-RC 13','',"Gerado em $($summary.generatedAt).",'',"Resumo: PASS=$($summary.passed) WARN=$($summary.warnings) FAIL_BLOCKING=$($summary.failedBlocking) STATUS=$($summary.statusFinal)",'','| Check | Status | Mensagem |','|---|---|---|')
foreach($r in $results){$md += "| $($r.name) | $($r.status) | $($r.message -replace '\|','/') |"}
$md|Set-Content -Encoding UTF8 docs/go-live-check-result.md
$summary|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 docs/go-live-check-result.json
if($summary.failedBlocking -gt 0){exit 1}; if($summary.warnings -gt 0 -and -not $AllowWarnings){exit 2}
