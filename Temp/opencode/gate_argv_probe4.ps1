[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$bat='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\echoargs.bat'
$f='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'
$long='http://localhost:5001/api/compras-empresariais/dashboard'

Write-Host '=== T1 var 4elem url curto'
$x=@('-s','--data-binary','@'+$f,'http://x'); $o=& cmd.exe /c $bat @x 2>&1 | Out-String; Write-Host $o

Write-Host '=== T2 var 4elem url longo'
$x=@('-s','--data-binary','@'+$f,$long); $o=& cmd.exe /c $bat @x 2>&1 | Out-String; Write-Host $o

Write-Host '=== T3 literal 4elem url longo'
$o=& cmd.exe /c $bat @('-s','--data-binary','@'+$f,$long) 2>&1 | Out-String; Write-Host $o
