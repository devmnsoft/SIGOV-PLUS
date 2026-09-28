[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$here = Split-Path $MyInvocation.MyCommand.Path -Parent
. (Join-Path $here 'jornada_lib.ps1')

$stg=Join-Path $here 'tmp'
if(-not (Test-Path $stg)){ New-Item -ItemType Directory -Path $stg | Out-Null }
$t=[guid]::NewGuid().ToString('N')
$jtmp=Join-Path $stg "ja_$t.json"; $htmp=Join-Path $stg "ja_$t.hdr"; $btmp=Join-Path $stg "ja_$t.body"
$json='{"urgencia":"NORMAL","itens":[],"version":999}'
[IO.File]::WriteAllText($jtmp,$json,(New-Object System.Text.UTF8Encoding($false)))
$uri="$($script:JBase)/requisicoes/$($script:Rq2)"
$cl='curl.exe -s -X PUT --max-time 90 -H "Authorization: Bearer ' + $script:TokA + '" -H "Host: municipio-demo.sigov.local"'
$cl += ' --data-binary @"' + $jtmp + '" -H "Content-Type: application/json; charset=utf-8"'
$cl += ' -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
$err=& cmd.exe /c $cl 2>&1 | Out-String
Write-Host ('curl exit: ' + $LASTEXITCODE)
if($err.Trim()){ Write-Host ('stderr: ' + $err.Trim()) }
$statusLine=(Get-Content $htmp -ErrorAction SilentlyContinue | Where-Object { $_ -match '^HTTP' } | Select-Object -First 1)
Write-Host ('STATUS: ' + $statusLine)
if(Test-Path $btmp){ Write-Host ('BODY: ' + [IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8)) }
foreach($f in @($jtmp,$htmp,$btmp)){ Remove-Item $f -Force -ErrorAction SilentlyContinue }
