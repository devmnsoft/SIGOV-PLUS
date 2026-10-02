import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
path = r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b_run9.log'
raw = open(path, 'rb').read()
if raw[:2] == b'\xff\xfe':
    text = raw.decode('utf-16-le', errors='replace')
elif raw[:3] == b'\xef\xbb\xbf':
    text = raw.decode('utf-8-sig', errors='replace')
else:
    text = raw.decode('utf-8', errors='replace')
lines = text.splitlines()
npass = sum(1 for ln in lines if 'ASSERT [PASS]' in ln)
nfail = sum(1 for ln in lines if 'ASSERT [FAIL]' in ln)
print('ASSERT_PASS=%d ASSERT_FAIL=%d' % (npass, nfail))
print('---- secoes ----')
for i, ln in enumerate(lines):
    if ln.startswith('===== S') or ln.startswith('==== GATE'):
        print('%d: %s' % (i+1, ln))
print('---- alertas (!! / ERRO / exception) ----')
for i, ln in enumerate(lines):
    if ('!!' in ln or 'ERRO' in ln.upper() or 'Exception' in ln) and 'ASSERT [PASS]' not in ln:
        print('%d: %s' % (i+1, ln[:200]))
print('---- cauda (a partir de S11) ----')
start = next(i for i, ln in enumerate(lines) if 'S11' in ln)
for i in range(start, len(lines)):
    print('%d: %s' % (i+1, lines[i]))
