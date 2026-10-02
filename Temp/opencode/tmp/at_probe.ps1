[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$token='jornada-analista-rc5068a-2026-sigov-demo-token'
$uri='http://localhost:5001/api/compras-empresariais/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001'
$stg='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp'
$t=[guid]::NewGuid().ToString('N')
$htmp=Join-Path $stg "bb_$t.hdr"; $btmp=Join-Path $stg "bb_$t.body"
$cl='curl.exe -s -X GET --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: municipio-demo.sigov.local" -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
& cmd.exe /c $cl 2>&1 | Out-Null
$bodytxt=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8)
Remove-Item $htmp,$btmp -Force -ErrorAction SilentlyContinue
$p=$bodytxt | ConvertFrom-Json
"direct: $($p.itens[0].GetType().FullName)"
$arr=@($p.itens)
"at-count: $($arr.Count)"
"at-types: $(($arr | ForEach-Object { $_.GetType().FullName }) -join ', ')"
"at0: [$($arr[0])]"
"at0 len: $(if($arr[0] -is [string]){$arr[0].Length}else{'n/a'})"
# e sem @():
$a2=$p.itens
"plain: $($a2.GetType().FullName) count=$($a2.Count)"
"a2[0]: $($a2[0].GetType().FullName)"
# teste com JSON inline identico em estrutura
$inline='{"a":[{"x":1,"y":"UN"},{"x":2,"y":"HORA"}]}' | ConvertFrom-Json
"inline at0: $((@($inline.a)[0]).GetType().FullName)"
