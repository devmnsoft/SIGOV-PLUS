import sys, io, re
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
tok = re.compile(r'\$([A-Za-z_][A-Za-z0-9_]*)')
for path in (r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1',
             r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_lib.ps1',
             r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset.ps1'):
    try:
        text = open(path, encoding='utf-8-sig').read()
    except Exception as ex:
        print(path, 'SKIP', ex); continue
    tag = path.split('\\')[-1]
    forms = {}
    for n, ln in enumerate(text.split('\n'), 1):
        for m in tok.finditer(ln):
            v = m.group(1)
            forms.setdefault(v.lower(), set()).add(v)
    dupes = {k: v for k, v in forms.items() if len(v) > 1}
    if not dupes:
        print(tag + ': no mixed-case duplicate variable names')
        continue
    for k, v in sorted(dupes.items()):
        print('== %s: formas %s' % (tag, sorted(v)))
        lines = text.split('\n')
        for n, ln in enumerate(lines, 1):
            if any(('$' + f) in ln for f in v):
                print('   L%d: %s' % (n, ln.strip()[:150]))
