#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MANIFEST="$ROOT/database/postgres/migrations/manifest.json"
python3 - "$ROOT" "$MANIFEST" <<'PY'
import hashlib,json,sys,pathlib
root=pathlib.Path(sys.argv[1]); data=json.loads(pathlib.Path(sys.argv[2]).read_text())
seen_v=set(); seen_f=set()
for e in data.get('migrations',[]):
    for k in ('version','description','category','file','checksum'):
        if not e.get(k): raise SystemExit(f'Entrada inválida: {e}')
    if e['version'] in seen_v or e['file'] in seen_f: raise SystemExit('Duplicidade no manifest')
    seen_v.add(e['version']); seen_f.add(e['file'])
    path=root/'database/postgres/migrations'/e['file']
    actual=hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != e['checksum']: raise SystemExit(f'Checksum divergente: {e["file"]}')
    print(e['file'])
PY
if [[ -n "${SIGOV_CONNECTION_STRING:-}" ]]; then
  while IFS= read -r file; do
    psql "$SIGOV_CONNECTION_STRING" -v ON_ERROR_STOP=1 -f "$ROOT/database/postgres/migrations/$file"
  done < <(python3 - "$MANIFEST" <<'PY'
import json,sys
for e in json.loads(open(sys.argv[1]).read()).get('migrations',[]):
    if e.get('applyAutomatically') is True: print(e['file'])
PY
)
fi
