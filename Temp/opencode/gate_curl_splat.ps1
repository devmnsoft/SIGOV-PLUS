[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$f='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'
[IO.File]::WriteAllText($f,'{"a":1}',(New-Object System.Text.UTF8Encoding($false)))
$url='http://localhost:5001/api/compras-empresariais/dashboard'
$h='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\hdr.txt'
$b='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\body.txt'

Write-Host '--- teste A: splat variavel normal'
$a=@('-s','-X','PUT','--max-time','30','--data-binary','@'+$f,'-H','Content-Type: application/json; charset=utf-8','-D',$h,'-o',$b,$url)
$e=& curl.exe @a 2>&1 | Out-String; Write-Host ("out=" + $e.Trim() + " exit=" + $LASTEXITCODE)

Write-Host '--- teste B: splat $args automatico'
$args=@('-s','-X','PUT','--max-time','30','--data-binary','@'+$f,'-H','Content-Type: application/json; charset=utf-8','-D',$h,'-o',$b,$url)
$e=& curl.exe @args 2>&1 | Out-String; Write-Host ("out=" + $e.Trim() + " exit=" + $LASTEXITCODE)

Write-Host '--- teste C: splat com Authorization e Host'
$a=@('-s','-X','PUT','--max-time','30','-H','Authorization: Bearer abc','-H','Host: municipio-demo.sigov.local','--data-binary','@'+$f,'-H','Content-Type: application/json; charset=utf-8','-D',$h,'-o',$b,$url)
$e=& curl.exe @a 2>&1 | Out-String; Write-Host ("out=" + $e.Trim() + " exit=" + $LASTEXITCODE)
