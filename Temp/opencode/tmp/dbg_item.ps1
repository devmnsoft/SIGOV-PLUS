[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$token='jornada-analista-rc5068a-2026-sigov-demo-token'
$uri='http://localhost:5001/api/compras-empresariais/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001'
$stg='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp'
$t=[guid]::NewGuid().ToString('N')
$htmp=Join-Path $stg "db_$t.hdr"; $btmp=Join-Path $stg "db_$t.body"
$cl='curl.exe -s -X GET --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: municipio-demo.sigov.local" -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
& cmd.exe /c $cl 2>&1 | Out-Null
$bodytxt=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8)
Remove-Item $htmp,$btmp -Force -ErrorAction SilentlyContinue
"bytes len: $($bodytxt.Length)"
$parsed=$bodytxt | ConvertFrom-Json
"top type: $($parsed.GetType().FullName)"
$it1=@($parsed.itens)[0]
"item type: $($it1.GetType().FullName)"
"item base: $(if($it1 -is [System.Management.Automation.PSObject]){$it1.BaseObject.GetType().FullName}else{'no-base'})"
if($it1 -is [System.Collections.IDictionary]){ "keys: $(($it1.Keys | Sort-Object) -join ', ')" }
"prop access: [$($it1.quantidade)]"
"dict access: [$(if($it1 -is [System.Collections.IDictionary]){$it1['quantidade']}else{'n/a'})]"
$out=$it1 | Out-String
"repr head: $($out.Substring(0,[Math]::Min(200,$out.Length)))"
