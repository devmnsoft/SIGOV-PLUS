[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$f='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'
$bat='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\echoargs.bat'
$url='http://localhost:5001/api/compras-empresariais/dashboard'

Write-Host '=== inline'
$o=& cmd.exe /c $bat -s --data-binary ('@'+$f) $url 2>&1 | Out-String
Write-Host $o

Write-Host '=== splat'
$a=@('-s','--data-binary','@'+$f,$url)
$o=& cmd.exe /c $bat @a 2>&1 | Out-String
Write-Host $o
