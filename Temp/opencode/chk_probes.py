import sys, io, glob
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
for path in sorted(glob.glob(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe*.ps1')):
    raw = open(path, 'rb').read()
    tag = path.split('\\')[-1]
    na = [(i, b) for i, b in enumerate(raw) if b > 127]
    boms = raw[:3] == b'\xef\xbb\xbf'
    print('%s  bytes=%d BOM=%s nonascii=%d' % (tag, len(raw), boms, len(na)))
    if na:
        print('   first nonascii:', na[:10])
# agora o detalhe exato do caso f do probe8 e caso j do probe9
for tag, start, end in (('probe8.ps1', "x-f", None), ('probe9.ps1', 'x-j', None)):
    raw = open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\\' + tag, 'rb').read()
    i = raw.find(start.encode())
    seg = raw[i:i+160]
    print('---', tag, 'around', start)
    print(seg.decode('ascii', errors='replace'))
    print('bytes:', ' '.join('%02x' % b for b in seg))
