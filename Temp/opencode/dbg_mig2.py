import hashlib, pathlib

p = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/migrations/20261001090000_integracao_outbox_base.sql')
txt = p.read_text(encoding='utf-8-sig').replace('\r\n', '\n').replace('\r', '\n')
sha = lambda s: hashlib.sha256(s.encode('utf-8')).hexdigest()

print('cur        ', sha(txt))
print('drop-first ', sha(txt[1:]))
print('strip      ', sha(txt.strip()))
print('rstrip     ', sha(txt.rstrip('\n')))
print('lstrip     ', sha(txt.lstrip()))

canon = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/script_completop.sql').read_text(encoding='utf-8-sig').replace('\r\n', '\n')
marker = '-- MIGRATION: 20261001090000_integracao_outbox_base.sql'
pos = canon.find(marker)
insB_at = canon.find("insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values ('20261001090000'")
# inserted block between last '====' header line and insB
hdr_end = canon.find('\n', canon.find('-- CHECKSUM_SHA256:', pos)) + 1
block = canon[hdr_end:insB_at].rstrip('\n')
print('---block head repr---')
print(repr(block[:130]))
print('block_sha    ', sha(block))
print('block == strip?', block == txt.strip())
if block != txt.strip():
    a, b = block, txt.strip()
    n = min(len(a), len(b))
    k = 0
    while k < n and a[k] == b[k]:
        k += 1
    print('first_diff_at', k)
    print('block   :', repr(a[max(0,k-20):k+40]))
    print('file    :', repr(b[max(0,k-20):k+40]))
    print('lens', len(a), len(b))
