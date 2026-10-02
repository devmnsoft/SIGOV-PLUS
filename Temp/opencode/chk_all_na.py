import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
raw = open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1', 'rb').read()
text = raw.decode('utf-8-sig')
lines = text.split('\n')
print('total_lines=%d' % len(lines))
found = False
for n, ln in enumerate(lines, 1):
    na = [(i, ch, hex(ord(ch))) for i, ch in enumerate(ln) if ord(ch) > 127]
    if na:
        found = True
        print('L%d: %s' % (n, na))
        # print context around each odd char
        for i, ch, hx in na[:5]:
            s = max(0, i-15); e = min(len(ln), i+15)
            print('    ctx[%d..%d] = %r' % (s, e, ln[s:e]))
if not found:
    print('ALL_ASCII=True')
# specifically hexdump region of line 234 around 'NUMC1'
ln234 = lines[233]
idx = ln234.find('NUMC1')
print('line234 len=%d numc1_at=%d' % (len(ln234), idx))
seg = ln234[max(0,idx-5):idx+12]
print('line234 seg repr = %r' % seg)
print('line234 seg bytes =', ' '.join('%02x' % ord(c) for c in seg))
