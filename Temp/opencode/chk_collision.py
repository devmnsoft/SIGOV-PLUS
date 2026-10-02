import sys, io, re, glob
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
pat = re.compile(r'\$([A-Za-z_][A-Za-z0-9_]*)\s*=')
tok = re.compile(r'\$([A-Za-z_][A-Za-z0-9_]*)')
files = [r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_b.ps1',
         r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_a.ps1',
         r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\jornada_lib.ps1',
         r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\gate_reset.ps1']
for path in files:
    try:
        text = open(path, encoding='utf-8-sig').read()
    except Exception as ex:
        print(path, 'SKIP', ex); continue
    tag = path.split('\\')[-1]
    hits = 0
    for n, ln in enumerate(text.split('\n'), 1):
        # apenas linhas com atribuicao dupla separada por ';'
        if ';' not in ln: continue
        parts = ln.split(';')
        if len(parts) < 2: continue
        # vars escritas em cada segmento (LHS da atribuicao)
        # vars lidas em cada segmento (qualquer $var que nao seja LHS)
        seg_lh = []   # (segment_index, var)
        seg_rd = []   # (segment_index, var)
        for i, p in enumerate(parts):
            for m in pat.finditer(p):
                seg_lh.append((i, m.group(1)))
            lhs = {m.group(1).lower() for m in pat.finditer(p)}
            for m in tok.finditer(p):
                v = m.group(1)
                if v.lower() in lhs: continue
                # descarta se e parte de uma expressao method? suficiente: so checa nome
                seg_rd.append((i, v))
        for si, w in seg_lh:
            # var escrita em segmento si pode ser lida em segmentos posteriores
            for ri, r in seg_rd:
                if ri > si and r.lower() == w.lower() and r != w:
                    print('%s L%d COLLIDE write=%s read_later=%s :: %s' % (tag, n, w, r, ln.strip()[:120]))
                    hits += 1
    print('%s collisions=%d' % (tag, hits))
