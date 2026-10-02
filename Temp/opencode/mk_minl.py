import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
core_lf = (
  "$h=@{ json=@{ id='a'; numero='n' } }\n"
  "Write-Output 'x'\n"
  "$A=[string]$h.json.id; $B=[string]$h.json.numero\n"
  "Write-Output ('minl: A=[' + $A + '] B=[' + $B + ']')\n"
)
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\minl_lf.ps1','wb').write(core_lf.encode('ascii'))
core_crlf = core_lf.replace('\n','\r\n')
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\minl_crlf.ps1','wb').write(core_crlf.encode('ascii'))
print('ok')
