$ErrorActionPreference='Stop'
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$c1.json.numero)){ Write-Output 'ASSERT-LIKE=PASS' }
$C1=[string]$c1.json.id; $NUMC1=[string]$c1.json.numero
Write-Output ('after assign: C1=[' + $C1 + '] NUMC1=[' + $NUMC1 + ']')
# agora perturba como no gate: atribuicoes subsequentes a $c1 (replay/mismatch)
$c1r=@{ status=201; json=@{ id='abc-123'; repetido=$true } }
$c1m=@{ status=409; body='conflito' }
$c1a=@{ status=422; body='ativa' }
Write-Output ('after clobber: C1=[' + $C1 + '] NUMC1=[' + $NUMC1 + ']')
# simula o uso em S8
$dpB=[pscustomobject]@{ numeroCotacao='CT-2026-000099' }
$bb4=[string]$dpB.numeroCotacao -eq $NUMC1
Write-Output ('bb4=' + $bb4)
