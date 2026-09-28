[Console]::OutputEncoding=[Text.Encoding]::UTF8
$sw=[Diagnostics.Stopwatch]::StartNew()
$out = docker exec sigov-postgres psql -U postgres -d sigov_gate_upgrade -v ON_ERROR_STOP=1 -f /tmp/gate_pre.sql 2>&1
$exit = $LASTEXITCODE
$sw.Stop()
$out | Out-String | Out-File -Encoding utf8 C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_upgrade_pre.log
"EXIT=$exit SEC=$([int]$sw.Elapsed.TotalSeconds)"
$out | Select-String -Pattern 'ERROR|FATAL' | Select-Object -First 5 | ForEach-Object { $_.Line }
