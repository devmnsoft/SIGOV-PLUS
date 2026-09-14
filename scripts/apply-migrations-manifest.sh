#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOST_NAME="${SIGOV_DB_HOST:-}"
DATABASE="${SIGOV_DB_NAME:-}"
USER_NAME="${SIGOV_DB_USER:-}"
MANIFEST_PATH="${MANIFEST_PATH:-database/postgres/migrations/manifest.json}"
VALIDATE_ONLY="${VALIDATE_ONLY:-false}"
MANIFEST="$ROOT/$MANIFEST_PATH"
python3 - "$ROOT" "$MANIFEST" <<'PY'
import hashlib,json,sys,pathlib
root=pathlib.Path(sys.argv[1]).resolve(); manifest_path=pathlib.Path(sys.argv[2]).resolve()
if not manifest_path.is_file(): raise SystemExit(f'Manifest não encontrado: {manifest_path}')
data=json.loads(manifest_path.read_text(encoding='utf-8-sig'))
seen_v=set(); seen_f=set()
sha256_pattern=__import__('re').compile(r'^[0-9a-f]{64}$')
def checksum(path):
    content=path.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('\r','\n')
    return hashlib.sha256(content.encode('utf-8')).hexdigest()
def compatibility_path(item):
    name=item.get('file','')
    if not name or pathlib.PurePath(name).name != name:
        raise SystemExit(f'Path de compatibilidade inválido: {name}')
    path=(root/'database/postgres/bootstrap'/name).resolve()
    bootstrap=(root/'database/postgres/bootstrap').resolve()
    if path.parent != bootstrap or not path.is_file():
        raise SystemExit(f'Compatibilidade ausente ou fora do bootstrap: {name}')
    if checksum(path) != item.get('checksum'):
        raise SystemExit(f'Checksum divergente na compatibilidade: {name}')
for e in data.get('migrations',[]):
    for k in ('version','description','category','file','checksum'):
        if not e.get(k): raise SystemExit(f'Entrada inválida: {e}')
    if e['version'] in seen_v or e['file'] in seen_f: raise SystemExit('Duplicidade no manifest')
    seen_v.add(e['version']); seen_f.add(e['file'])
    if not sha256_pattern.fullmatch(e['checksum']):
        raise SystemExit(f'Checksum SHA-256 inválido: {e["file"]}')
    path=root/'database/postgres/migrations'/e['file']
    if not path.is_file(): raise SystemExit(f'Migration ausente: {e["file"]}')
    if checksum(path) != e['checksum']: raise SystemExit(f'Checksum divergente: {e["file"]}')
    known=e.get('knownChecksums') or []
    if len(known) != len(set(known)):
        raise SystemExit(f'knownChecksums duplicados: {e["file"]}')
    if any(not isinstance(value,str) or not sha256_pattern.fullmatch(value) for value in known):
        raise SystemExit(f'knownChecksums contém SHA-256 inválido: {e["file"]}')
    if e['checksum'] in known:
        raise SystemExit(f'Checksum atual repetido em knownChecksums: {e["file"]}')
    if known and not str(e.get('postConditionSql') or '').strip():
        raise SystemExit(f'POSTCONDITION_MISSING: knownChecksums exige postConditionSql em {e["file"]}')
    probe_names=set()
    for probe in e.get('postConditionProbes') or []:
        name=str(probe.get('name') or '').strip(); sql=str(probe.get('sql') or '').strip()
        if not name or not sql:
            raise SystemExit(f'postConditionProbe sem nome ou SQL: {e["file"]}')
        if name in probe_names:
            raise SystemExit(f'postConditionProbe duplicada em {e["file"]}: {name}')
        probe_names.add(name)
    compatibility_seen=set()
    for item in e.get('compatibilityBefore') or []:
        if item.get('file') in compatibility_seen:
            raise SystemExit(f'Compatibilidade duplicada em {e["file"]}: {item.get("file", "")}')
        compatibility_seen.add(item.get('file'))
        compatibility_path(item)
    if e.get('applyAutomatically') is True: print(f"{e['version']}|{e['file']}|{e['category']}|{e['checksum']}")
for item in data.get('compatibilityAfterAll') or []: compatibility_path(item)
PY
if [[ "$VALIDATE_ONLY" == "true" || -z "$HOST_NAME" || -z "$DATABASE" || -z "$USER_NAME" ]]; then exit 0; fi

# A execução histórica deste wrapper aplicava todos os arquivos sem consultar o
# ledger, sem registrar a versão atomicamente e sem avaliar pós-condições. Isso
# torna uma reaplicação ou um upgrade inseguro. Até haver paridade integral com o
# runner canônico, falhe de forma explícita em vez de alterar o banco parcialmente.
printf '%s\n' 'BLOCKED: execução de migrations pelo wrapper Bash não possui contrato seguro de ledger/pós-condições. Use scripts/apply-migrations-manifest.ps1.' >&2
exit 2
