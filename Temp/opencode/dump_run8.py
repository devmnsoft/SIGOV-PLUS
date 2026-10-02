import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
path = r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b_run8.log'
raw = open(path, 'rb').read()
if raw[:2] == b'\xff\xfe':
    text = raw.decode('utf-16-le', errors='replace')
elif raw[:3] == b'\xef\xbb\xbf':
    text = raw.decode('utf-8-sig', errors='replace')
else:
    text = raw.decode('utf-8', errors='replace')
lines = text.splitlines()
print('TOTAL_LINES=%d' % len(lines))
print('---- FAILS ----')
for i, ln in enumerate(lines):
    if 'ASSERT [FAIL]' in ln:
        print('%d: %s' % (i+1, ln))
print('---- DBG ----')
for i, ln in enumerate(lines):
    if ln.startswith('DBG-'):
        print('%d: %s' % (i+1, ln))
print('---- RESUMO area ----')
for i, ln in enumerate(lines):
    if 'S11' in ln or 'RESUMO' in ln or 'FAILS=' in ln or 'PASS=' in ln or 'TOTAL=' in ln:
        print('%d: %s' % (i+1, ln))
