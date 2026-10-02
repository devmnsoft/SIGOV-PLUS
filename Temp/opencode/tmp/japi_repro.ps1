[Console]::OutputEncoding=[System.Text.Encoding]::UTF8
. (Join-Path $PSScriptRoot '..\jornada_lib.ps1')
$script:JDbMode='none'
$r=JApi 'GET' '/cotacoes/elaboracao/d0000001-0000-4000-8000-000000000001' $script:TokA
"status: $($r.status)"
"body head: $(($r.body).Substring(0,[Math]::Min(120,$r.body.Length)))"
"json null? $($null -eq $r.json)"
$e1=$r.json
"e1 null? $($null -eq $e1)"
"itens count: $(@($e1.itens).Count)"
$it1=@($e1.itens)[0]
"it1 null? $($null -eq $it1)"
if($it1){
  "props: $(($it1 | Get-Member -MemberType NoteProperty | ForEach-Object { $_.Name }) -join ', ')"
  "quantidade: [$($it1.quantidade)] type=$(if($it1.quantidade -ne $null){$it1.quantidade.GetType().Name}else{'null'})"
}
