import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
raw = open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1', 'rb').read()
text = raw.decode('utf-8-sig')
lines = text.split('\n')
for n in (234, 394):
    ln = lines[n-1]
    print('L%d repr = %r' % (n, ln))
    print('L%d bytes:' % n, ' '.join('%02x' % b for b in ln.encode('utf-8')))
# find every occurrence of bytes that look like 'NUMC1' with context bytes
data = raw
i = 0
count = 0
while True:
    i = data.find(b'NUMC1', i)
    if i < 0: break
    count += 1
    print('occurrence %d at byte %d, context bytes:' % (count, i), ' '.join('%02x' % b for b in data[i-6:i+8]))
    i += 1
print('total NUMC1 occurrences:', count)
