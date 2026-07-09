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
$docs=@('README.md','docs/matriz-modulos-release-candidate.md','docs/diagnostico-pos-rc-07.md','docs/go-live-pos-rc-07.md','docs/homologacao-executavel-pos-rc-07.md','docs/tenant-context-pos-rc-07.md','docs/security-lgpd-pos-rc-07.md','docs/ci-cd-pos-rc-06.md','docs/smoke-e2e-pos-rc-06.md')
foreach($d in $docs){Check-Path "doc:$d" $d}

if(Test-Path 'README.md'){ $readme=Get-Content 'README.md' -Raw; if($readme -match 'Pós-RC 06' -and $readme -match 'Pós-RC 07'){Add-Check 'readme-pos-rc-06-07' 'PASS' 'README contém Pós-RC 06/07.'}else{Add-Check 'readme-pos-rc-06-07' 'FAIL' 'README deve mencionar Pós-RC 06 e Pós-RC 07.'} }
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
$md=@('# Go-live check Pós-RC 07','',"Gerado em $($summary.generatedAt).",'',"Resumo: PASS=$($summary.passed) WARN=$($summary.warnings) FAIL_BLOCKING=$($summary.failedBlocking) STATUS=$($summary.statusFinal)",'','| Check | Status | Mensagem |','|---|---|---|')
foreach($r in $results){$md += "| $($r.name) | $($r.status) | $($r.message -replace '\|','/') |"}
$md|Set-Content -Encoding UTF8 docs/go-live-check-result.md
$summary|ConvertTo-Json -Depth 8|Set-Content -Encoding UTF8 docs/go-live-check-result.json
if($summary.failedBlocking -gt 0){exit 1}; if($summary.warnings -gt 0 -and -not $AllowWarnings){exit 2}
