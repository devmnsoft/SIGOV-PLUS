import sys, io
sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
BASE = "@{ status=201; json=@{ id='abc-123'; numero='CT-2026-000099'; rodada=1 } }"
JTH  = "@{ json=@{ id='id-j'; numero='num-j' } }"
IF_X = "if($false){ Write-Output 'x' }"
IF_XJ = "if($false){ Write-Output 'x-j' }"
blocks = []

def blk(tag, src, ht, iff, a1, a2, p1, p2):
    L = []
    L.append('%s=%s' % (src, ht))
    if iff: L.append(iff)
    L.append('%s; %s' % (a1, a2))
    L.append("Write-Output ('%s: P1=[' + %s + '] P2=[' + %s + ']')" % (tag, p1, p2))
    blocks.append('\n'.join(L))
    blocks.append('')

# w1 controle puro (copyd renomeado) - esperado PASS
blk('w1 ctrl   ', '$q1', BASE, IF_X, '$W1p=[string]$q1.json.id', '$W1s=[string]$q1.json.numero', '$W1p', '$W1s')
# w2 apenas valor numero trocado p/ num-j
blk('w2 val-j  ', '$q2', "@{ status=201; json=@{ id='abc-123'; numero='num-j'; rodada=1 } }", IF_X, '$W2p=[string]$q2.json.id', '$W2s=[string]$q2.json.numero', '$W2p', '$W2s')
# w3 apenas literal do if trocado p/ x-j
blk('w3 iff-xj ', '$q3', BASE, IF_XJ, '$W3p=[string]$q3.json.id', '$W3s=[string]$q3.json.numero', '$W3p', '$W3s')
# w4 apenas nomes destino estilo j ($Cj/$Nj) com conteudo BASE
blk('w4 nms-j  ', '$q4', BASE, IF_X, '$Cj4=[string]$q4.json.id', '$Nj4=[string]$q4.json.numero', '$Cj4', '$Nj4')
# w5 fonte+hasht completo do j, nomes destino neutros
blk('w5 data-j ', '$cj5', JTH, IF_XJ, '$W5p=[string]$cj5.json.id', '$W5s=[string]$cj5.json.numero', '$W5p', '$W5s')
# w6 copia exata do j (fontes e destinos originais) - esperado FAIL
blk('w6 full-j ', '$cj6', JTH, IF_XJ, '$Cj6x=[string]$cj6.json.id', '$Nj6x=[string]$cj6.json.numero', '$Cj6x', '$Nj6x')
# w7 dados do j sem linha if
blk('w7 j-noif ', '$q7', JTH, None, '$W7p=[string]$q7.json.id', '$W7s=[string]$q7.json.numero', '$W7p', '$W7s')
# w8 BASE com if e SEM cast (prova se cast esta no gatilho)
blk('w8 nocast ', '$q8', BASE, IF_X, '$W8p=$q8.json.id', '$W8s=$q8.json.numero', '$W8p', '$W8s')
text = '\n'.join(blocks)
open(r'C:\MNSOFT\SIGOV-PLUS\Temp\opencode\probe_bisect.ps1','wb').write(text.replace('\n','\r\n').encode('ascii'))
print('written ok')
