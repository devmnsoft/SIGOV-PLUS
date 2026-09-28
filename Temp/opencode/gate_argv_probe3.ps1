[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$bat='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\echoargs.bat'
$p='@C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'

Write-Host '=== 2 elems (db, @p)'
$o=& cmd.exe /c $bat '--data-binary' $p 2>&1 | Out-String; Write-Host $o
$o=& cmd.exe /c $bat @('--data-binary',$p) 2>&1 | Out-String; Write-Host $o

Write-Host '=== 3 elems (-s, db, @p)'
$o=& cmd.exe /c $bat @('-s','--data-binary',$p) 2>&1 | Out-String; Write-Host $o

Write-Host '=== inline -s db @p url'
$o=& cmd.exe /c $bat -s '--data-binary' $p 'http://x' 2>&1 | Out-String; Write-Host $o
