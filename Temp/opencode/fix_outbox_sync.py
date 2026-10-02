import hashlib, pathlib

OLD = '9bdc416f13cf0ef208f9ff7938e56bb3b166888ac697be3aec7a27cc5b13d078'
NEW = '6534bafcfc445f028c6f14a4249935a15041c618d7dd3ca91e372a52430c53c4'

p = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/migrations/20261001090000_integracao_outbox_base.sql')
raw = p.read_bytes()
sha_raw = hashlib.sha256(raw).hexdigest()
txt = p.read_text(encoding='utf-8-sig').replace('\r\n', '\n').replace('\r', '\n')
sha_norm = hashlib.sha256(txt.encode('utf-8')).hexdigest()
print('raw==norm?', sha_raw == sha_norm, '->', NEW if sha_norm == NEW else 'MISMATCH ' + sha_norm)
assert sha_norm == NEW

cons = [
    'C:/MNSOFT/SIGOV-PLUS/script_completo.sql',
    'C:/MNSOFT/SIGOV-PLUS/script_completop.sql',
    'C:/MNSOFT/SIGOV-PLUS/script_completo_dev.sql',
    'C:/MNSOFT/SIGOV-PLUS/database/script_completo.sql',
    'C:/MNSOFT/SIGOV-PLUS/database/postgres/script_completo.sql',
    'C:/MNSOFT/SIGOV-PLUS/database/postgres/script_completo_dev.sql',
]

# sanity: inserted block in one canonical file differs only by the first dash
canon = pathlib.Path(cons[1]).read_bytes()
marker = b'-- MIGRATION: 20261001090000_integracao_outbox_base.sql'
pos = canon.find(marker)
insB_at = canon.find(b"insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values ('20261001090000'")
sep_start = canon.find(b'\n', canon.find(b'-- CHECKSUM_SHA256:', pos)) + 1   # start of 2nd '====' line
hdr_end = canon.find(b'\n', sep_start) + 1                                   # start of body
block = canon[hdr_end:insB_at].rstrip(b'\n').decode('utf-8')
print('block==txt[1:].strip()?', block == txt[1:].strip())
assert block == txt[1:].strip()

for f in cons:
    b = pathlib.Path(f).read_bytes()
    n_sha = b.count(OLD.encode())
    n_dash = b.count(b'\n- Correcao aditiva: tabela base do outbox transacional de integracao')
    print('SHA_COUNT=%d DASH_COUNT=%d %s' % (n_sha, n_dash, f.split('/')[-1]))
    assert n_sha == 3 and n_dash == 1, f
    b = b.replace(OLD.encode(), NEW.encode())
    b = b.replace(b'\n- Correcao aditiva: tabela base do outbox transacional de integracao',
                  b'\n-- Correcao aditiva: tabela base do outbox transacional de integracao')
    pathlib.Path(f).write_bytes(b)
    print('  fixed ->', pathlib.Path(f).name)

mf = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/migrations/manifest.json')
mb = mf.read_bytes()
n = mb.count(OLD.encode())
print('MANIFEST_SHA_COUNT=', n)
assert n == 1
mf.write_bytes(mb.replace(OLD.encode(), NEW.encode()))
print('  fixed -> manifest.json')
print('FIX_DONE')
