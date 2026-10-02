
$h1=@{ json=@{ id='i1'; numero='n1' } }
$A1=[string]$h1.json.id; $B1=[string]$h1.json.numero
Write-Output ('v1 noper-out : A=[' + $A1 + '] B=[' + $B1 + ']')

$h2=@{ json=@{ id='i2'; numero='n2' } }
Write-Output 'x2'
$A2=[string]$h2.json.id; $B2=[string]$h2.json.numero
Write-Output ('v2 perout-one : A=[' + $A2 + '] B=[' + $B2 + ']')

$h3=@{ json=@{ id='i3'; numero='n3' } }
Write-Output 'x3'
$A3=[string]$h3.json.id
$B3=[string]$h3.json.numero
Write-Output ('v3 perout-two : A=[' + $A3 + '] B=[' + $B3 + ']')

$h4=@{ json=@{ id='i4'; numero='n4' } }
$x4 = Write-Output 'x4'
$A4=[string]$h4.json.id; $B4=[string]$h4.json.numero
Write-Output ('v4 captured-out : A=[' + $A4 + '] B=[' + $B4 + ']')

$h5=@{ json=@{ id='i5'; numero='n5' } }
'x5' | Out-Null
$A5=[string]$h5.json.id; $B5=[string]$h5.json.numero
Write-Output ('v5 out-null     : A=[' + $A5 + '] B=[' + $B5 + ']')
