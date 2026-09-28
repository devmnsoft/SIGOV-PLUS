[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$f='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'
[IO.File]::WriteAllText($f,'{"a":1}',(New-Object System.Text.UTF8Encoding($false)))
$url='http://localhost:5001/api/compras-empresariais/dashboard'

Write-Host '--- teste 1: arg unico com @'
$r=& curl.exe -s -o NUL -w '%{http_code}' --data-binary ('@'+$f) $url 2>&1; Write-Host ("out=" + $r + " exit=" + $LASTEXITCODE)

Write-Host '--- teste 2: -d em vez de --data-binary'
$r=& curl.exe -s -o NUL -w '%{http_code}' -d ('@'+$f) $url 2>&1; Write-Host ("out=" + $r + " exit=" + $LASTEXITCODE)

Write-Host '--- teste 3: via cmd /c'
$r=& cmd /c "curl.exe -s -o NUL -w %{http_code} --data-binary @`"$f`" $url" 2>&1; Write-Host ("out=" + $r + " exit=" + $LASTEXITCODE)

Write-Host '--- teste 4: sem -w, so exit code'
$r=& curl.exe -s --data-binary ('@'+$f) $url 2>&1 | Out-String; Write-Host ("out=" + $r.Trim() + " exit=" + $LASTEXITCODE)
