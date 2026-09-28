[Console]::OutputEncoding=[Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$repo='C:\MNSOFT\SIGOV-PLUS'
$td="$repo\Temp\opencode"
$db='sigov_gate_upgrade'

function RunSql {
  param([string]$DbName,[string]$RemoteFile,[string]$Log)
  $o = docker exec sigov-postgres psql -U postgres -d $DbName -v ON_ERROR_STOP=1 -A -F ' | ' -f $RemoteFile 2>&1
  $e = $LASTEXITCODE
  ($o | Out-String) | Out-File -Encoding utf8 $Log
  return [PSCustomObject]@{Exit=$e; Out=($o | ForEach-Object { $_.ToString() })}
}

# S1: fixture legado (pre-migration)
docker cp "$td\gate_legacy_fixture.sql" sigov-postgres:/tmp/gate_legacy_fixture.sql | Out-Null
$r1 = RunSql -DbName $db -RemoteFile /tmp/gate_legacy_fixture.sql -Log "$td\gate_upgrade_fixture.log"
"S1 FIXTURE EXIT=$($r1.Exit)"
$r1.Out | Where-Object { $_ -match 'FIXTURE_DIV|FIXTURE_COLS' } | ForEach-Object { '   ' + $_.TrimEnd() }

# S2: migration 20260927 (raw)
$o2 = docker exec sigov-postgres psql -U postgres -d $db -v ON_ERROR_STOP=1 -f /tmp/gate_20260927.sql 2>&1
$e2 = $LASTEXITCODE
($o2 | Out-String) | Out-File -Encoding utf8 "$td\gate_upgrade_mig.log"
"S2 MIGRATION EXIT=$e2"

# S3: asserts pos-migration
docker cp "$td\gate_upg_assert.sql" sigov-postgres:/tmp/gate_upg_assert.sql | Out-Null
$r3 = RunSql -DbName $db -RemoteFile /tmp/gate_upg_assert.sql -Log "$td\gate_upgrade_assert1.txt"
"S3 ASSERT EXIT=$($r3.Exit)"
$r3.Out | Where-Object { $_ } | ForEach-Object { '   ' + $_.TrimEnd() }

# S4: neg1 - ENCERRADA sem codigo (esperado EXIT=1, ck_comp_recb_div_codigo_encerrado)
docker cp "$td\gate_upg_neg1.sql" sigov-postgres:/tmp/gate_upg_neg1.sql | Out-Null
$n1 = docker exec sigov-postgres psql -U postgres -d $db -v ON_ERROR_STOP=1 -f /tmp/gate_upg_neg1.sql 2>&1
$en1 = $LASTEXITCODE
"NEG1_EXIT=$en1 (esperado 1)"
$n1 | ForEach-Object { "$_" } | Where-Object { $_ -match 'ERROR|check' } | Select-Object -First 2 | ForEach-Object { '   ' + $_.TrimEnd() }

# S5: neg2 - codigo fora do catalogo (esperado EXIT=1, ck_comp_recb_div_resultado_codigo)
docker cp "$td\gate_upg_neg2.sql" sigov-postgres:/tmp/gate_upg_neg2.sql | Out-Null
$n2 = docker exec sigov-postgres psql -U postgres -d $db -v ON_ERROR_STOP=1 -f /tmp/gate_upg_neg2.sql 2>&1
$en2 = $LASTEXITCODE
"NEG2_EXIT=$en2 (esperado 1)"
$n2 | ForEach-Object { "$_" } | Where-Object { $_ -match 'ERROR|check' } | Select-Object -First 2 | ForEach-Object { '   ' + $_.TrimEnd() }

# S6: reaplicacao no-op
$o6 = docker exec sigov-postgres psql -U postgres -d $db -v ON_ERROR_STOP=1 -f /tmp/gate_20260927.sql 2>&1
$e6 = $LASTEXITCODE
($o6 | Out-String) | Out-File -Encoding utf8 "$td\gate_upgrade_mig_noop.log"
"S6 NOOP EXIT=$e6"

# S7: assert de novo e compara
$r7 = RunSql -DbName $db -RemoteFile /tmp/gate_upg_assert.sql -Log "$td\gate_upgrade_assert2.txt"
"ASSERT_COMPARE_DIFFS=$((Compare-Object (Get-Content "$td\gate_upgrade_assert1.txt") (Get-Content "$td\gate_upgrade_assert2.txt") | Measure-Object).Count) (esperado 0)"
