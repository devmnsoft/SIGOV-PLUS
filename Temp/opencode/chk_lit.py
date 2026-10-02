import sys
files = {
    'gate': r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1',
    'probe1': r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe_fails.ps1',
    'probe2': r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe2.ps1',
}
needles = [b'ENCERRADA', b'SELECIONADA', b'a0000001-0000-4000-8000-000000000201', b'*Beta*']
for name, path in files.items():
    data = open(path, 'rb').read()
    print('==== %s (%d bytes, BOM=%s)' % (name, len(data), data[:3] == b'\xef\xbb\xbf'))
    for n in needles:
        idx = 0
        cnt = 0
        while True:
            i = data.find(n, idx)
            if i < 0: break
            cnt += 1
            chunk = data[max(0, i-8): i+len(n)+8]
            nonascii = [(o, hex(b)) for o, b in enumerate(chunk) if b > 127]
            print('  %-40s at +%d: %r  nonascii=%s' % (n.decode(), i, chunk, nonascii))
            idx = i + 1
        if cnt == 0:
            print('  %-40s NOT FOUND' % n.decode())
print('CHK_DONE')
