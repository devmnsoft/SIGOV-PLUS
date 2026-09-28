[Console]::OutputEncoding=[Text.Encoding]::UTF8
$src='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_pre_20260927.sql'
$dst='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_pre_fixed.sql'
$t=[IO.File]::ReadAllLines($src)
[IO.File]::WriteAllLines($dst, $t + @('', '\endif', ''))
"SRC_LINES=$($t.Count)"
docker cp $dst sigov-postgres:/tmp/gate_pre_fixed.sql | Out-Null
docker exec sigov-postgres psql -U postgres -A -t -c "select count(*) from pg_database where datname='sigov_gate_upgrade'" | Out-Null
docker exec sigov-postgres psql -U postgres -A -t -c "drop database if exists sigov_gate_upgrade;" | Out-Null
docker exec sigov-postgres psql -U postgres -A -t -c "create database sigov_gate_upgrade;" | Out-Null
$sw=[Diagnostics.Stopwatch]::StartNew()
$out = docker exec sigov-postgres psql -U postgres -d sigov_gate_upgrade -v ON_ERROR_STOP=1 -f /tmp/gate_pre_fixed.sql 2>&1
$exit=$LASTEXITCODE
$sw.Stop()
$out | Out-String | Out-File -Encoding utf8 'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_upgrade_pre.log'
"EXIT=$exit SEC=$([int]$sw.Elapsed.TotalSeconds)"
$out | Select-String -Pattern 'ERROR|FATAL' | Select-Object -First 5 | ForEach-Object { $_.Line }
