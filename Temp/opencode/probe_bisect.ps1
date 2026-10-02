$q1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if($false){ Write-Output 'x' }
$W1p=[string]$q1.json.id; $W1s=[string]$q1.json.numero
Write-Output ('w1 ctrl   : P1=[' + $W1p + '] P2=[' + $W1s + ']')

$q2=@{ status=201; json=@{ id='abc-123'; numero='num-j'; rodada=1 } }
if($false){ Write-Output 'x' }
$W2p=[string]$q2.json.id; $W2s=[string]$q2.json.numero
Write-Output ('w2 val-j  : P1=[' + $W2p + '] P2=[' + $W2s + ']')

$q3=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if($false){ Write-Output 'x-j' }
$W3p=[string]$q3.json.id; $W3s=[string]$q3.json.numero
Write-Output ('w3 iff-xj : P1=[' + $W3p + '] P2=[' + $W3s + ']')

$q4=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if($false){ Write-Output 'x' }
$Cj4=[string]$q4.json.id; $Nj4=[string]$q4.json.numero
Write-Output ('w4 nms-j  : P1=[' + $Cj4 + '] P2=[' + $Nj4 + ']')

$cj5=@{ json=@{ id='id-j'; numero='num-j' } }
if($false){ Write-Output 'x-j' }
$W5p=[string]$cj5.json.id; $W5s=[string]$cj5.json.numero
Write-Output ('w5 data-j : P1=[' + $W5p + '] P2=[' + $W5s + ']')

$cj6=@{ json=@{ id='id-j'; numero='num-j' } }
if($false){ Write-Output 'x-j' }
$Cj6x=[string]$cj6.json.id; $Nj6x=[string]$cj6.json.numero
Write-Output ('w6 full-j : P1=[' + $Cj6x + '] P2=[' + $Nj6x + ']')

$q7=@{ json=@{ id='id-j'; numero='num-j' } }
$W7p=[string]$q7.json.id; $W7s=[string]$q7.json.numero
Write-Output ('w7 j-noif : P1=[' + $W7p + '] P2=[' + $W7s + ']')

$q8=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if($false){ Write-Output 'x' }
$W8p=$q8.json.id; $W8s=$q8.json.numero
Write-Output ('w8 nocast : P1=[' + $W8p + '] P2=[' + $W8s + ']')
