[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$f='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp\probe.json'
[IO.File]::WriteAllText($f,'{"a":1}',(New-Object System.Text.UTF8Encoding($false)))
$url='http://localhost:5001/api/compras-empresariais/dashboard'

function T($label,$arr){
  $e=& curl.exe @arr 2>&1 | Out-String
  Write-Host ("{0} => exit={1} :: {2}" -f $label,$LASTEXITCODE,($e.Trim().Substring(0,[Math]::Min(110,$e.Trim().Length))))
}
T '1 base'            @('-s','--data-binary','@'+$f,$url)
T '2 +XPUT'           @('-s','-X','PUT','--data-binary','@'+$f,$url)
T '3 +maxtime'        @('-s','-X','PUT','--max-time','30','--data-binary','@'+$f,$url)
T '4 +contenttype'    @('-s','-X','PUT','--max-time','30','--data-binary','@'+$f,'-H','Content-Type: application/json; charset=utf-8',$url)
T '5 +authhdr'        @('-s','-X','PUT','--max-time','30','-H','Authorization: Bearer abc','--data-binary','@'+$f,$url)
T '6 +hostr'          @('-s','-X','PUT','--max-time','30','-H','Host: municipio-demo.sigov.local','--data-binary','@'+$f,$url)
