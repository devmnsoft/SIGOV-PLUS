import pathlib

p = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/migrations/20261001090000_integracao_outbox_base.sql')
txt = p.read_text(encoding='utf-8-sig').replace('\r\n', '\n').replace('\r', '\n')

canon = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/script_completop.sql').read_bytes().decode('utf-8').replace('\r\n', '\n')
marker = '-- MIGRATION: 20261001090000_integracao_outbox_base.sql'
pos = canon.find(marker)
insB_at = canon.find("insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values ('20261001090000'")
hdr_end = canon.find('\n', canon.find('-- CHECKSUM_SHA256:', pos)) + 1
block = canon[hdr_end:insB_at].rstrip('\n')

exp = txt[1:].strip()
print('lens', len(block), len(exp))
k = 0
n = min(len(block), len(exp))
while k < n and block[k] == exp[k]:
    k += 1
print('first_diff_at', k)
print('BLOCK:', repr(block[max(0, k - 30):k + 50]))
print('EXP  :', repr(exp[max(0, k - 30):k + 50]))
print('block tail:', repr(block[-80:]))
print('exp   tail:', repr(exp[-80:]))
