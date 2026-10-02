import io
path = r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1'
raw = open(path, 'rb').read()
# strip BOM if present
text = raw.decode('utf-8-sig')
lines = text.split('\n')
for n in (391, 417, 449):
    line = lines[n-1]
    na = [(i, ch, hex(ord(ch))) for i, ch in enumerate(line) if ord(ch) > 127]
    print('line %d len=%d nonascii=%s' % (n, len(line), na))
    # also flag any zero-width / unusual whitespace chars anywhere
    odd = [(i, hex(ord(ch))) for i, ch in enumerate(line) if ord(ch) in (0xa0, 0x200b, 0x200c, 0x200d, 0xfeff, 0x2018, 0x2019, 0x201c, 0x201d)]
    if odd:
        print('   odd chars: %s' % odd)
print('OK')
