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
root=pathlib.Path(sys.argv[1]).resolve(); manifest=pathlib.Path(sys.argv[2]).resolve()
migrations_root=(root/'database/postgres/migrations').resolve()
if not manifest.is_file(): raise SystemExit(f'Manifest não encontrado: {manifest}')
data=json.loads(manifest.read_text(encoding='utf-8'))
seen_v=set(); seen_f=set()
for e in data.get('migrations',[]):
    for k in ('version','description','category','file','checksum'):
        if not e.get(k): raise SystemExit(f'Entrada inválida: {e}')
    if e['version'] in seen_v or e['file'] in seen_f: raise SystemExit('Duplicidade no manifest')
    seen_v.add(e['version']); seen_f.add(e['file'])
    path=(migrations_root/e['file']).resolve()
    if path.parent != migrations_root: raise SystemExit(f'Path de migration inválido: {e["file"]}')
    if not path.is_file(): raise SystemExit(f'Migration ausente: {e["file"]}')
    content=path.read_text(encoding='utf-8-sig').replace('\r\n','\n').replace('\r','\n')
    if hashlib.sha256(content.encode('utf-8')).hexdigest() != e['checksum']: raise SystemExit(f'Checksum divergente: {e["file"]}')
PY
if [[ "$VALIDATE_ONLY" == "true" ]]; then
  printf 'Manifesto validado; nenhuma conexão ou migration foi executada.\n'
  exit 0
fi
if [[ -z "$HOST_NAME" || -z "$DATABASE" || -z "$USER_NAME" ]]; then
  printf 'CONFIGURATION_REQUIRED: defina SIGOV_DB_HOST, SIGOV_DB_NAME e SIGOV_DB_USER; ou use VALIDATE_ONLY=true.\n' >&2
  exit 2
fi
if ! command -v psql >/dev/null 2>&1; then
  printf 'MISSING_TOOL: psql não encontrado no PATH.\n' >&2
  exit 2
fi

# Este runner Bash histórico não mantém o ledger nem executa as pós-condições do
# manifesto. Executá-lo contra um banco poderia reaplicar migrations e relatar um
# sucesso diferente do MigrationRunner/.NET e do aplicador PowerShell canônicos.
# Fail closed até que compartilhe exatamente o mesmo contrato transacional.
printf 'UNSAFE_RUNNER_BLOCKED: execução de banco pelo aplicador Bash está desabilitada; use scripts/apply-migrations-manifest.ps1.\n' >&2
exit 2
