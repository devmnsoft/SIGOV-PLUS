#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HOST_NAME="${SIGOV_DB_HOST:-}"
PORT="${SIGOV_DB_PORT:-5432}"
DATABASE="${SIGOV_DB_NAME:-}"
USER_NAME="${SIGOV_DB_USER:-}"
MANIFEST_PATH="${MANIFEST_PATH:-database/postgres/migrations/manifest.json}"
VALIDATE_ONLY="${VALIDATE_ONLY:-false}"
SSL_MODE="${SIGOV_DB_SSLMODE:-}"
MANIFEST="$ROOT/$MANIFEST_PATH"
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
    if hashlib.sha256(path.read_bytes()).hexdigest() != e['checksum']: raise SystemExit(f'Checksum divergente: {e["file"]}')
    if e.get('applyAutomatically') is True: print(f"{e['version']}|{e['file']}|{e['category']}|{e['checksum']}")
PY
if [[ "$VALIDATE_ONLY" == "true" || -z "$HOST_NAME" || -z "$DATABASE" || -z "$USER_NAME" ]]; then exit 0; fi
export PGSSLMODE="$SSL_MODE"
while IFS='|' read -r version file category checksum; do
  start=$(date -u +%Y-%m-%dT%H:%M:%SZ); begin=$(date +%s%3N); result=success; sanitized=""
  if ! output=$(psql -h "$HOST_NAME" -p "$PORT" -U "$USER_NAME" -d "$DATABASE" -v ON_ERROR_STOP=1 -f "$ROOT/database/postgres/migrations/$file" 2>&1); then
    result=failed; sanitized=$(printf '%s' "$output" | sed -E 's#postgres(ql)?://[^[:space:]]+#postgres://***#g; s#(password|pwd)=([^;[:space:]]+)#\1=***#Ig')
  fi
  end=$(date -u +%Y-%m-%dT%H:%M:%SZ); finish=$(date +%s%3N); duration=$((finish-begin))
  printf '{"version":"%s","file":"%s","category":"%s","checksum":"%s","startedAt":"%s","finishedAt":"%s","durationMs":%s,"result":"%s","error":"%s"}\n' "$version" "$file" "$category" "$checksum" "$start" "$end" "$duration" "$result" "${sanitized//\"/\\\"}" >> "$ROOT/migration.log"
  [[ "$result" == success ]] || { printf '%s\n' "$sanitized" >&2; exit 1; }
done < <(python3 - "$MANIFEST" <<'PY'
import json,sys
for e in json.loads(open(sys.argv[1]).read()).get('migrations',[]):
    if e.get('applyAutomatically') is True: print(f"{e['version']}|{e['file']}|{e['category']}|{e['checksum']}")
PY
)
