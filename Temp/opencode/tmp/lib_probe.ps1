[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$token='jornada-analista-rc5068a-2026-sigov-demo-token'
$uri='http://localhost:5001/api/compras-empresariais/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001'
$stg='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp'
$t=[guid]::NewGuid().ToString('N')
$htmp=Join-Path $stg "cc_$t.hdr"; $btmp=Join-Path $stg "cc_$t.body"
$cl='curl.exe -s -X GET --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: municipio-demo.sigov.local" -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
& cmd.exe /c $cl 2>&1 | Out-Null
$bodytxt=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8)
Remove-Item $htmp,$btmp -Force -ErrorAction SilentlyContinue
$p=$bodytxt | ConvertFrom-Json
"ANTES LIB: at0 type=$((@($p.itens)[0]).GetType().FullName) qtd=[((@($p.itens)[0]).quantidade)]"
. (Join-Path $PSScriptRoot '..\jornada_lib.ps1')
"DEPOIS LIB: at0 type=$((@($p.itens)[0]).GetType().FullName) qtd=[((@($p.itens)[0]).quantidade)]"
"alias ConvertFrom-Json: $(Get-Alias ConvertFrom-Json -ErrorAction SilentlyContinue | Out-String)"
"func Get-Member local: $(Get-Command Get-Member -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source)"
