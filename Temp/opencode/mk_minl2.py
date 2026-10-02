import io, sys
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
# exatamente o caso do gate, em .ps1, tanto LF quanto CRLF
core = (
  "$c1=@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }\n"
  "if(-not [string]::IsNullOrWhiteSpace([string]$c1.json.numero)){ Write-Output 'ASSERT-LIKE=PASS' }\n"
  "$C1=[string]$c1.json.id; $NUMC1=[string]$c1.json.numero\n"
  "Write-Output ('minl2: C1=[' + $C1 + '] NUMC1=[' + $NUMC1 + ']')\n"
)
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\minl2_lf.ps1','wb').write(core.encode('ascii'))
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\minl2_crlf.ps1','wb').write(core.replace('\n','\r\n').encode('ascii'))
print('ok')
