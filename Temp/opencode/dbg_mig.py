import hashlib, json, pathlib

p = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/migrations/20261001090000_integracao_outbox_base.sql')
raw = p.read_bytes()
print('file_len', len(raw), 'first8', raw[:8].hex(), 'has_cr', b'\r' in raw)
txt = p.read_text(encoding='utf-8-sig').replace('\r\n', '\n').replace('\r', '\n')
print('file_sha', hashlib.sha256(txt.encode('utf-8')).hexdigest())

m = json.loads(pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/migrations/manifest.json').read_text(encoding='utf-8-sig'))
for e in m['migrations']:
    if e['version'] == '20261001090000':
        print('manifest_sha', e['checksum'])
        print('manifest_keys', sorted(e.keys()))

canon = pathlib.Path('C:/MNSOFT/SIGOV-PLUS/database/postgres/script_completo.sql').read_text(encoding='utf-8-sig').replace('\r\n', '\n')
marker = '-- MIGRATION: 20261001090000_integracao_outbox_base.sql'
pos = canon.find(marker)
print('marker_pos', pos)
print('---around marker---')
print(canon[pos - 20 : pos + 320])
body = txt.strip()
print('body_in_canon_from_pos', body in canon[pos:])
if body not in canon[pos:]:
    # locate longest common prefix position
    head = body[:80]
    j = canon.find(head, pos)
    print('head_find_at', j)
    if j >= 0:
        k = 0
        while k < len(body) and canon[j + k] == body[k]:
            k += 1
        print('common_prefix_len', k)
        print('expected_next', repr(body[k:k + 60]))
        print('actual_next', repr(canon[j + k:j + k + 60]))
    # also show tail region of the inserted block
    ins_end = canon.find('insert into sigov.schema_migrations(version, description, checksum, category, source, success, execution_ms, applied_at) values (\'20261001090000\'')
    print('insB_at', ins_end)
    print(canon[ins_end - 260:ins_end])
