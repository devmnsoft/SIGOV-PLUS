[Console]::OutputEncoding=[Text.Encoding]::UTF8
$t=[IO.File]::ReadAllLines('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_pre_20260927.sql')
'LINES='+$t.Count
$null = $t[-8..-1] | ForEach-Object { $_ }
