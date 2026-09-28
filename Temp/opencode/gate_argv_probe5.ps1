[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$bat='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\echoargs.bat'
$f='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'

Write-Host '=== A: @path ULTIMO (4 elems)'
$o=& cmd.exe /c $bat @('-s','--data-binary','http://x','@'+$f) 2>&1 | Out-String; Write-Host $o

Write-Host '=== B: sem @, prefixo X (4 elems)'
$o=& cmd.exe /c $bat @('-s','--data-binary','x'+$f,'http://x') 2>&1 | Out-String; Write-Host $o

Write-Host '=== C: 5 elems, @path ultimo'
$o=& cmd.exe /c $bat @('-s','b','c','d','@'+$f) 2>&1 | Out-String; Write-Host $o

Write-Host '=== D: elemento ja cotado com espaco antes do @'
$o=& cmd.exe /c $bat @('-s','--data-binary',' @'+$f,'http://x') 2>&1 | Out-String; Write-Host $o
