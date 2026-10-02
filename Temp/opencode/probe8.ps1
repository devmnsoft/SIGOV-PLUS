# e: if com Write-Output; atribuicoes em linhas separadas
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$c1.json.numero)){ Write-Output 'x-e' }
$Ce=[string]$c1.json.id
$Ne=[string]$c1.json.numero
Write-Output ('e: C=[' + $Ce + '] N=[' + $Ne + ']')

# f: Write-Output solto antes (sem if)
$cf=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
Write-Output 'x-f'
$Cf=[string]$cf.json.id; $Nf=[string]$cf.json.numero
Write-Output ('f: C=[' + $Cf + '] N=[' + $Nf + ']')

# g: if com Write-Output em duas linhas (bloco aberto/fechado separado)
$cg=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$cg.json.numero)) {
  Write-Output 'x-g'
}
$Cg=[string]$cg.json.id; $Ng=[string]$cg.json.numero
Write-Output ('g: C=[' + $Cg + '] N=[' + $Ng + ']')

# h: if com Write-Output, atribuiçao dupla SEM cast no segundo
$ch=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$ch.json.numero)){ Write-Output 'x-h' }
$Ch=[string]$ch.json.id; $Nh=$ch.json.numero
Write-Output ('h: C=[' + $Ch + '] N=[' + $Nh + ']')

# i: if com Write-Output, sem cast nenhum
$ci=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$ci.json.numero)){ Write-Output 'x-i' }
$Ci=$ci.json.id; $Ni=$ci.json.numero
Write-Output ('i: C=[' + $Ci + '] N=[' + $Ni + ']')
