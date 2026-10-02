$ErrorActionPreference='Continue'
$H=@{Host='municipio-demo.sigov.local'; Authorization='Bearer jornada-analista-rc5068a-2026-sigov-demo-token'}
$base='http://localhost:5001/api/compras-empresariais'
function JCall($m,$path,$body,$key){
 $hdr=@{}; $H.GetEnumerator() | ForEach-Object { $hdr[$_.Key]=$_.Value }
 if($key){ $hdr['Idempotency-Key']=$key }
 if($body){ $hdr['Content-Type']='application/json' }
 $uri="$base$path"
 try{ $r=Invoke-WebRequest -Method $m -Uri $uri -Headers $hdr -Body $body -UseBasicParsing -TimeoutSec 30; $code=[int]$r.StatusCode; $txt=$r.Content }
 catch { $resp=$_.Exception.Response; if($null -eq $resp){ Write-Host "$m $path -> EXC $($_.Exception.Message)"; return }; $code=[int]$resp.StatusCode; $s=$resp.GetResponseStream(); $sr=New-Object System.IO.StreamReader($s); $txt=$sr.ReadToEnd() }
 if($null -eq $txt){ $txt='' }
 if($txt.Length -gt 350){ $txt=$txt.Substring(0,350) }
 Write-Host ("{0} {1} -> {2} : {3}" -f $m,$path,$code,$txt)
}
Write-Host '--- probe 1: GET /pedidos (esperado 200 vazio)'
JCall 'GET' '/pedidos?pagina=1&tamanho=10' $null $null
Write-Host '--- probe 2: GET /recebimentos (esperado 200 vazio)'
JCall 'GET' '/recebimentos?pagina=1&tamanho=10' $null $null
Write-Host '--- probe 3: POST /fornecedores sem documento (esperado 400)'
JCall 'POST' '/fornecedores' '{"tipoPessoa":"PJ","razaoSocial":"Probe Sem Docs Ltda"}' 'probe-b-forn-1'
Write-Host '--- probe 4: elaboracao Rq1 + catalogo + fornecedores'
$e=(Invoke-WebRequest -Uri "$base/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001" -Headers $H -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json
$i1=@($e.itens)[0]; $i2=@($e.itens)[1]
Write-Host ("itens: {0} / {1}" -f $i1.requisicaoItemId,$i2.requisicaoItemId)
$prods=(Invoke-WebRequest -Uri "$base/cotacoes/produtos?limite=50" -Headers $H -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json
$pA=[string]$prods[0].id; $pB=[string]$prods[1].id
Write-Host ("prods: {0} / {1}" -f $pA,$pB)
$fq=(Invoke-WebRequest -Uri "$base/fornecedores?busca=" -Headers $H -UseBasicParsing -TimeoutSec 30).Content | ConvertFrom-Json
Write-Host ("fornecedores visiveis: {0}" -f (@($fq.items).Count))
$sql1="select id from sigov.compras_empresarial_fornecedor where codigo='FORN-DEMO-2026-0001';"
$sql2="select id from sigov.compras_empresarial_fornecedor where codigo='FORN-DEMO-2026-0002';"
$f1=(docker exec sigov-postgres psql -U postgres -d postgres -t -A -c $sql1).Trim()
$f2=(docker exec sigov-postgres psql -U postgres -d postgres -t -A -c $sql2).Trim()
Write-Host ("f1=$f1 f2=$f2")
Write-Host '--- probe 5: POST /cotacoes IT1+IT2 (esperado 201)'
$prazo=(Get-Date).AddDays(7).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
$body=[ordered]@{ requisicaoId='d0000001-0000-4000-8000-000000000001'; fornecedorIds=@($f1,$f2); prazo=$prazo; produtos=@([ordered]@{requisicaoItemId=[string]$i1.requisicaoItemId; produtoId=$pA},[ordered]@{requisicaoItemId=[string]$i2.requisicaoItemId; produtoId=$pB}) }
$json=$body | ConvertTo-Json -Depth 6 -Compress
Write-Host $json
JCall 'POST' '/cotacoes' $json 'probe-b-cot-create-1'
Write-Host 'PROBES_CONCLUIDOS'
