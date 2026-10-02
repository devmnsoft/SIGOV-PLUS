$h2=@{ json=@{ id='i2'; numero='n2' } }
Write-Output 'x2'
$A2=[string]$h2.json.id; $B2=[string]$h2.json.numero
Write-Output ('lf : A=[' + $A2 + '] B=[' + $B2 + ']')
