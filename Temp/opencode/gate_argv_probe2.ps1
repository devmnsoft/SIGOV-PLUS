[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$bat='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\echoargs.bat'

Write-Host '=== array contents'
$a=@('-s','--data-binary','@C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json','http://x')
Write-Host ("count=" + $a.Count)
foreach($x in $a){ Write-Host ("ELEM [" + $x + "]") }

Write-Host '=== bat inline com @path'
$o=& cmd.exe /c $bat '@C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json' 2>&1 | Out-String
Write-Host $o

Write-Host '=== bat splat com @path'
$b=@('@C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json')
$o=& cmd.exe /c $bat @b 2>&1 | Out-String
Write-Host $o

Write-Host '=== bat splat path sem @'
$c=@('C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json')
$o=& cmd.exe /c $bat @c 2>&1 | Out-String
Write-Host $o
