[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$repo='C:\MNSOFT\SIGOV-PLUS'
$td="$repo\Temp\opencode"

function RunSql {
  param([string]$DbName,[string]$RemoteFile,[string]$Log)
  $o = docker exec sigov-postgres psql -U postgres -d $DbName -v ON_ERROR_STOP=1 -A -F ' | ' -f $RemoteFile 2>&1
  $e = $LASTEXITCODE
  ($o | Out-String) | Out-File -Encoding utf8 $Log
  return [PSCustomObject]@{Exit=$e; Out=($o | ForEach-Object { $_.ToString() })}
}
function Cpu([string]$Local,[string]$Remote) { docker cp "$Local" "sigov-postgres:$Remote" | Out-Null }

Write-Host '===== PART A: HEAL DEMO (sigov_gate_a_empty) ====='
docker exec sigov-postgres psql -U postgres -c 'drop database if exists sigov_gate_a_empty;' -c 'create database sigov_gate_a_empty;' | Out-Null
$o = docker exec sigov-postgres psql -U postgres -d sigov_gate_a_empty -v ON_ERROR_STOP=1 -f /tmp/gate_pre_fixed.sql 2>&1
($o | Out-String) | Out-File -Encoding utf8 "$td\gate_heal_pre.log"
"A1 PRE_STATE EXIT=$LASTEXITCODE (esperado 0)"

Cpu "$td\gate_heal_sim.sql" /tmp/gate_heal_sim.sql
$r = RunSql -DbName sigov_gate_a_empty -RemoteFile /tmp/gate_heal_sim.sql -Log "$td\gate_heal_sim.log"
"A2 SIM_EXIT=$($r.Exit) (esperado 0)"
$r.Out | Where-Object { $_ -match '^SIM_' } | ForEach-Object { '   ' + $_.TrimEnd() }

Cpu "$td\gate_heal_bug.sql" /tmp/gate_heal_bug.sql
$ob = docker exec sigov-postgres psql -U postgres -d sigov_gate_a_empty -v ON_ERROR_STOP=1 -A -F ' | ' -f /tmp/gate_heal_bug.sql 2>&1
$eb = $LASTEXITCODE
($ob | Out-String) | Out-File -Encoding utf8 "$td\gate_heal_bug.log"
"A3 BUG_DEMO EXIT=$eb (esperado != 0: varchar(24) rejeita 27 chars)"
$ob | Where-Object { $_ -match 'ERROR|too long' } | Select-Object -First 2 | ForEach-Object { '   ' + $_.TrimEnd() }

$om = docker exec sigov-postgres psql -U postgres -d sigov_gate_a_empty -v ON_ERROR_STOP=1 -f /tmp/gate_20260927.sql 2>&1
($om | Out-String) | Out-File -Encoding utf8 "$td\gate_heal_mig.log"
"A4 MIGRATION_EXIT=$LASTEXITCODE (esperado 0)"

Cpu "$td\gate_heal_postfix.sql" /tmp/gate_heal_postfix.sql
Cpu "$td\gate_heal_assert.sql" /tmp/gate_heal_assert.sql
$op = docker exec sigov-postgres psql -U postgres -d sigov_gate_a_empty -v ON_ERROR_STOP=1 -A -F ' | ' -f /tmp/gate_heal_postfix.sql 2>&1
($op | Out-String) | Out-File -Encoding utf8 "$td\gate_heal_postfix.log"
"A5 POSTFIX_EXIT=$LASTEXITCODE (esperado 0)"
$r = RunSql -DbName sigov_gate_a_empty -RemoteFile /tmp/gate_heal_assert.sql -Log "$td\gate_heal_assert.txt"
"A6 ASSERT_EXIT=$($r.Exit)"
$r.Out | Where-Object { $_ } | ForEach-Object { '   ' + $_.TrimEnd() }

Write-Host '===== PART B: CLEAN INSTALL REBUILD (sigov_gate_clean) ====='
Cpu "$repo\script_completo_dev.sql" /tmp/gate_dev.sql
docker exec sigov-postgres psql -U postgres -c 'drop database if exists sigov_gate_clean;' -c 'create database sigov_gate_clean;' | Out-Null
$o = docker exec sigov-postgres psql -U postgres -d sigov_gate_clean -v ON_ERROR_STOP=1 -f /tmp/gate_dev.sql 2>&1
($o | Out-String) | Out-File -Encoding utf8 "$td\gate_clean_install2.log"
"B1 INSTALL EXIT=$LASTEXITCODE (esperado 0)"

$o = docker exec sigov-postgres psql -U postgres -d sigov_gate_clean -v ON_ERROR_STOP=1 -f /tmp/gate_20260927.sql 2>&1
($o | Out-String) | Out-File -Encoding utf8 "$td\gate_clean_noop2.log"
"B2 NOOP_REAPPLY EXIT=$LASTEXITCODE (esperado 0)"

Cpu "$td\gate_clean_probe2.sql" /tmp/gate_clean_probe2.sql
$r = RunSql -DbName sigov_gate_clean -RemoteFile /tmp/gate_clean_probe2.sql -Log "$td\gate_clean_probe2.txt"
"B3 PROBE_EXIT=$($r.Exit)"
$r.Out | Where-Object { $_ } | ForEach-Object { '   ' + $_.TrimEnd() }
