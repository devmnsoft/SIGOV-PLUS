import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')

def show(tag, marker, nbytes=220):
    raw = open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\\'+tag,'rb').read()
    i = raw.find(marker.encode())
    seg = raw[max(0,i-80):i+nbytes]
    print('=====', tag, 'around', marker)
    print(repr(seg.decode('ascii', errors='replace')))
    print()

show('probe8.ps1', 'x-f')
show('probe_matrix.ps1', 'x2')
show('probe7.ps1', "if($false){ Write-Output 'x' }")
show('probe9.ps1', 'x-j')
# checa BOM de todos
import glob
for p in sorted(glob.glob(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe*.ps1')) + [r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1']:
    raw = open(p,'rb').read()
    tag = p.split('\\')[-1]
    print(tag, 'BOM=%s first16=%s CRLFcount=%d LFtotal=%d' % (raw[:3]==b'\xef\xbb\xbf', raw[:4].hex(), raw.count(b'\r\n'), raw.count(b'\n')))
