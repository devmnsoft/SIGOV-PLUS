# a: exato probe5
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$c1.json.numero)){ Write-Output 'ASSERT-LIKE=PASS' }
$C1=[string]$c1.json.id; $NUMC1=[string]$c1.json.numero
Write-Output ('a: C1=[' + $C1 + '] NUMC1=[' + $NUMC1 + ']')

# b: sem a linha if
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
$C1b=[string]$c1.json.id; $NUMC1b=[string]$c1.json.numero
Write-Output ('b: C1=[' + $C1b + '] NUMC1=[' + $NUMC1b + ']')

# c: com a linha if mas variaveis B
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$c1.json.numero)){ }
$Cb=[string]$c1.json.id; $Nb=[string]$c1.json.numero
Write-Output ('c: C=[' + $Cb + '] N=[' + $Nb + ']')

# d: if que NAO executa o body
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if($false){ Write-Output 'x' }
$Cd=[string]$c1.json.id; $Nd=[string]$c1.json.numero
Write-Output ('d: C=[' + $Cd + '] N=[' + $Nd + ']')
