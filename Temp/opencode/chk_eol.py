import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
for tag in ('probe8.ps1','probe_matrix.ps1','minl_lf.ps1','gate_b.ps1'):
    raw = open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\\'+tag,'rb').read()
    crlf = raw.count(b'\r\n')
    lf = raw.count(b'\n') - crlf
    print('%-20s bytes=%-7d BOM=%s CRLF=%d loneLF=%d' % (tag, len(raw), raw[:3]==b'\xef\xbb\xbf', crlf, lf))
