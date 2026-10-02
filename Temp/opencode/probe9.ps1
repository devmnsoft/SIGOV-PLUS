# j: if(false) com Write-Output no corpo + atrib dupla cast
$cj=@{ json=@{ id='id-j'; numero='num-j' } }
if($false){ Write-Output 'x-j' }
$Cj=[string]$cj.json.id; $Nj=[string]$cj.json.numero
Write-Output ('j: C=[' + $Cj + '] N=[' + $Nj + ']')

# k: Write-Output solto + atrib dupla cast
$ck=@{ json=@{ id='id-k'; numero='num-k' } }
Write-Output 'x-k'
$Ck=[string]$ck.json.id; $Nk=[string]$ck.json.numero
Write-Output ('k: C=[' + $Ck + '] N=[' + $Nk + ']')

# l: if executado com Write-Host + atrib dupla cast
$cl=@{ json=@{ id='id-l'; numero='num-l' } }
if($true){ Write-Host 'x-l' }
$Cl=[string]$cl.json.id; $Nl=[string]$cl.json.numero
Write-Output ('l: C=[' + $Cl + '] N=[' + $Nl + ']')

# m: if executado com Write-Output em variavel + atrib dupla cast
$cm=@{ json=@{ id='id-m'; numero='num-m' } }
$x=(if($true){ 'x-m' })
$Cm=[string]$cm.json.id; $Nm=[string]$cm.json.numero
Write-Output ('m: C=[' + $Cm + '] N=[' + $Nm + ']')

# n: mesmo exato do gate (nome NUMC1) com if(false)
$cn=@{ json=@{ id='id-n'; numero='num-n' } }
if($false){ Write-Output 'x-n' }
$Cn=[string]$cn.json.id; $NUMCN=[string]$cn.json.numero
Write-Output ('n: C=[' + $Cn + '] NUMCN=[' + $NUMCN + ']')
