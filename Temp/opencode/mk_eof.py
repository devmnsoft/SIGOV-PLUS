import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
core = (
  "$h2=@{ json=@{ id='i2'; numero='n2' } }\n"
  "Write-Output 'x2'\n"
  "$A2=[string]$h2.json.id; $B2=[string]$h2.json.numero\n"
  "Write-Output ('lf : A=[' + $A2 + '] B=[' + $B2 + ']')\n"
)
# arquivo LF
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\eof_lf.ps1','wb').write(core.encode('ascii'))
# arquivo CRLF
crlf = core.replace('\n','\r\n')
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\eof_crlf.ps1','wb').write(crlf.encode('ascii'))
print('written lf=%d crlf=%d' % (len(core), len(crlf)))
