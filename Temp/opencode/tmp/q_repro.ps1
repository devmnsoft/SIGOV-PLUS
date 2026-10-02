[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
$j='{"requisicaoId":"d0000001-0000-4000-8000-000000000001","status":"APROVADA","elegivel":true,"proximaRodada":1,"itens":[{"requisicaoItemId":"d0000001-0000-4000-8000-000000000101","ordem":1,"unidade":"UN","quantidade":10.0000,"reservaAtiva":0,"saldoDisponivel":10.0000,"exigeInspecao":false},{"requisicaoItemId":"d0000001-0000-4000-8000-000000000102","ordem":2,"unidade":"HORA","quantidade":5.0000,"reservaAtiva":0,"saldoDisponivel":5.0000,"exigeInspecao":false}]}'
$e1=$j | ConvertFrom-Json
$it1=@($e1.itens)[0]; $it2=@($e1.itens)[1]
"it1 null? $($null -eq $it1)"
"it1.quantidade raw: [$($it1.quantidade)] type=$(if($it1){$it1.quantidade.GetType().Name}else{'n/a'})"
$Q1=[decimal]$it1.quantidade; $Q2=[decimal]$it2.quantidade
"Q1=$Q1 Q2=$Q2"
"it1.requisicaoItemId: $($it1.requisicaoItemId)"
