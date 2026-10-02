import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
body = r"""
# copy-d (exato do probe7 caso d)
$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if($false){ Write-Output 'x' }
$Cd=[string]$c1.json.id; $Nd=[string]$c1.json.numero
Write-Output ('copyd: C=[' + $Cd + '] N=[' + $Nd + ']')

# copy-j (exato do probe9 caso j)
$cj=@{ json=@{ id='id-j'; numero='num-j' } }
if($false){ Write-Output 'x-j' }
$Cj=[string]$cj.json.id; $Nj=[string]$cj.json.numero
Write-Output ('copyj: C=[' + $Cj + '] N=[' + $Nj + ']')

# copy-a (exato do probe7 caso a)
$ca=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }
if(-not [string]::IsNullOrWhiteSpace([string]$ca.json.numero)){ Write-Output 'ASSERT-LIKE=PASS' }
$Ca=[string]$ca.json.id; $Na=[string]$ca.json.numero
Write-Output ('copya: C=[' + $Ca + '] N=[' + $Na + ']')
"""
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe_samefile.ps1','w',encoding='ascii',newline='\r\n').write(body)
print('written')
