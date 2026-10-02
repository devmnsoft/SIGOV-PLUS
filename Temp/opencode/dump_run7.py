import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
for name in ('gate_reset_run7.log', 'gate_b_run7.log'):
    path = r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\\' + name
    raw = open(path, 'rb').read()
    enc = None
    if raw[:2] == b'\xff\xfe':
        enc = 'utf-16-le'
    elif raw[:3] == b'\xef\xbb\xbf':
        enc = 'utf-8-sig'
    else:
        enc = 'utf-8'
    text = raw.decode(enc, errors='replace')
    lines = text.splitlines()
    print('==== %s  enc=%s bytes=%d lines=%d' % (name, enc, len(raw), len(lines)))
    for ln in lines[-60:]:
        print(ln)
    print('END_OF_PREVIEW')
