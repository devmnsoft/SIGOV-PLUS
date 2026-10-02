import io
code = "$h=@{ json=@{ id='a'; numero='n' } }\r\nWrite-Output 'x'\r\n$A=[string]$h.json.id; $B=[string]$h.json.numero\r\n"
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\ast_case.txt','wb').write(code.encode('ascii'))
print('wrote %d bytes' % len(code))
