[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$ErrorActionPreference='Continue'
$token='jornada-analista-rc5068a-2026-sigov-demo-token'
$uri='http://localhost:5001/api/compras-empresariais/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001'
$stg='C:\MNSOFT\SIGOV-PLUS\Temp\opencode\tmp'
function Fetch($t){
  $htmp=Join-Path $stg "zz_$t.hdr"; $btmp=Join-Path $stg "zz_$t.body"
  $cl='curl.exe -s -X GET --max-time 90 -H "Authorization: Bearer ' + $token + '" -H "Host: municipio-demo.sigov.local" -D "' + $htmp + '" -o "' + $btmp + '" "' + $uri + '"'
  & cmd.exe /c $cl 2>&1 | Out-Null
  $b=[IO.File]::ReadAllText($btmp,[System.Text.Encoding]::UTF8)
  Remove-Item $htmp,$btmp -Force -ErrorAction SilentlyContinue
  return $b
}
$b1=Fetch ([guid]::NewGuid().ToString('N'))
$p1=$b1 | ConvertFrom-Json
"a: p1.itens type: $($p1.itens.GetType().FullName) count=$($p1.itens.Count)"
"a: p1.itens[0] type: $($p1.itens[0].GetType().FullName)"
$a1=@($p1.itens)[0]
"a: @()[0] type: $($a1.GetType().FullName) quantidade=[$($a1.quantidade)]"
# agora a mesma leitura passando pelo caminho do gate_b: hashtable .json
$h=@{ status=200; body=$b1; json=$p1 }
$e1=$h.json
"b: e1.itens[0] type: $($e1.itens[0].GetType().FullName)"
# segunda requisicao fresca
$b2=Fetch ([guid]::NewGuid().ToString('N'))
"bodies iguais? $($b1 -ceq $b2)"
if($b1 -cne $b2){
  for($i=0;$i -lt [Math]::Min($b1.Length,$b2.Length);$i++){ if($b1[$i] -cne $b2[$i]){ "primeira diferenca em byte $i : [$($b1.Substring([Math]::Max(0,$i-40)))] vs [$($b2.Substring([Math]::Max(0,$i-40)))]"; break } }
}
$p2=$b2 | ConvertFrom-Json
"b: p2.itens[0] type: $($p2.itens[0].GetType().FullName)"
# extrai o trecho ao redor de '"itens"' do corpo bruto
$i=$b1.IndexOf('"itens"')
"raw around itens: [$($b1.Substring($i,[Math]::Min(120,$b1.Length-$i)))]"
