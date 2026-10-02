$ErrorActionPreference='Stop'
$B='http://localhost:5001/api/compras-empresariais'
function JCall([string]$m,[string]$u,[string]$tok,$bd,$key){
  $h=@{ Authorization="Bearer $tok"; Host='municipio-demo.sigov.local' }
  if($key){ $h['Idempotency-Key']=$key }
  try {
    if($bd -ne $null){ $r=Invoke-WebRequest -Uri $u -Method $m -Headers $h -Body ($bd|ConvertTo-Json -Depth 8) -ContentType 'application/json' -UseBasicParsing }
    else { $r=Invoke-WebRequest -Uri $u -Method $m -Headers $h -UseBasicParsing }
    return @{ s=[int]$r.StatusCode.Value__; b=$r.Content }
  } catch {
    $rs=$_.Exception.Response
    if($null -eq $rs){ return @{ s=-1; b=$_.Exception.Message } }
    $sr=New-Object System.IO.StreamReader($rs.GetResponseStream())
    return @{ s=[int]$rs.StatusCode; b=$sr.ReadToEnd() }
  }
}
$TA='jornada-analista-rc5068a-2026-sigov-demo-token'
$C1='c337b50c-f5b8-43f7-bbe2-e85e9c242561'
$cvAlfa='136f2796-9a95-4496-8125-75948bb4cb3f'

$r=JCall 'GET' "$B/pedidos" $TA $null $null
Write-Output ("P1 GET /pedidos => http={0} :: {1}" -f $r.s, $r.b.Substring(0,[Math]::Min(120,$r.b.Length)))

$dd=(JCall 'GET' "$B/cotacoes/$C1" $TA $null $null).b | ConvertFrom-Json
$it1=@($dd.itens)[0].requisicaoItemId
$it2=@($dd.itens)[1].requisicaoItemId
Write-Output ("P2 detalhe C1 => status={0} statusCotacao={1} convites={2}" -f $dd.status, $dd.status, @($dd.convites).Count)

$stale=@{ conviteId=$cvAlfa; conviteVersion=999; itens=@(@{requisicaoItemId=$it1; precoUnitario=100; desconto=0; imposto=0; frete=25; prazoDias=10; recusado=$false}) }
$r=JCall 'POST' "$B/cotacoes/$C1/respostas" $TA $stale 'jb-probe-stale-fx1'
Write-Output ("P3 responder stale => http={0} msg-ok={1}" -f $r.s, ($r.b -like '*Versão desatualizada; recarregue o detalhe da cotação e tente novamente.*'))

$fn=@{ tipoPessoa='J'; documento='12345678000195'; razaoSocial='Fornecedor Probe Fictício LTDA'; nomeFantasia=$null; categoria=$null; porte=$null; condicaoPagamento=$null; prazoMedio=15; observacoes=$null }
$r=JCall 'POST' "$B/fornecedores" $TA $fn 'jb-probe-fn-fx1'
$idOut=''
try { $idOut=([string]($r.b | ConvertFrom-Json).id) } catch { }
Write-Output ("P4 POST /fornecedores (nomeFantasia=null) => http={0} id={1}" -f $r.s, $idOut)
