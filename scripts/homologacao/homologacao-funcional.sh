#!/usr/bin/env bash
set -Eeuo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
ARTIFACT_DIR="${SIGOV_HOMOLOGACAO_ARTIFACT_DIR:-$ROOT/artifacts/homologacao}"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
REPORT="$ARTIFACT_DIR/homologacao-$STAMP.jsonl"
DB_CONNECTION="${SIGOV_DB_CONNECTION:-host=localhost port=5432 dbname=postgres user=postgres password=123456 options=--search_path=sigov}"
WEB_URL="${SIGOV_WEB_URL:-http://127.0.0.1:5080}"
API_URL="${SIGOV_API_URL:-http://127.0.0.1:5081}"
START_RUNTIME="${SIGOV_START_RUNTIME:-1}"
PIDS=()
FAILED=0

mkdir -p "$ARTIFACT_DIR"
chmod 700 "$ARTIFACT_DIR"

record() {
  local phase="$1" status="$2" detail="$3"
  detail="${detail//\\/\\\\}"; detail="${detail//\"/\\\"}"; detail="${detail//$'\n'/ }"
  printf '{"timestamp":"%s","phase":"%s","status":"%s","detail":"%s"}\n' \
    "$(date -u +%FT%TZ)" "$phase" "$status" "$detail" >> "$REPORT"
}

finish() {
  local pid
  for pid in "${PIDS[@]:-}"; do kill "$pid" 2>/dev/null || true; done
  wait "${PIDS[@]:-}" 2>/dev/null || true
  chmod 600 "$REPORT" 2>/dev/null || true
  (( FAILED == 0 )) || exit 1
}
trap finish EXIT INT TERM

for tool in dotnet psql pg_dump pg_restore curl python3; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    record prerequisites BLOCKED "ferramenta ausente: $tool"
    printf 'P0 ambiental: ferramenta ausente: %s\n' "$tool" >&2
    exit 2
  fi
done
record prerequisites PASS "ferramentas obrigatorias disponiveis"

cd "$ROOT"
if ! psql "$DB_CONNECTION" -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql >>"$ARTIFACT_DIR/database-$STAMP.log" 2>&1; then
  record database FAIL "script_completo_dev.sql falhou; consulte database-$STAMP.log"; exit 1
fi
record database PASS "script_completo_dev.sql aplicado"
if ! psql "$DB_CONNECTION" -v ON_ERROR_STOP=1 -f database/postgres/seeds/seed_homologacao_funcional.sql >>"$ARTIFACT_DIR/seed-$STAMP.log" 2>&1; then
  record seed FAIL "seed_homologacao_funcional.sql falhou; consulte seed-$STAMP.log"; exit 1
fi
record seed PASS "seed funcional aplicado"

dotnet restore sigov.runtime.slnf --locked-mode >>"$ARTIFACT_DIR/build-$STAMP.log" 2>&1
dotnet build sigov.runtime.slnf -c Release --no-restore --nologo -warnaserror >>"$ARTIFACT_DIR/build-$STAMP.log" 2>&1
record build PASS "runtime Release compilado com warnings como erros"

if [[ "$START_RUNTIME" == 1 ]]; then
  ASPNETCORE_URLS="$API_URL" dotnet run --project src/Sigov.Api --no-build -c Release >>"$ARTIFACT_DIR/api-$STAMP.log" 2>&1 & PIDS+=("$!")
  ASPNETCORE_URLS="$WEB_URL" dotnet run --project src/Sigov.Web --no-build -c Release >>"$ARTIFACT_DIR/web-$STAMP.log" 2>&1 & PIDS+=("$!")
  dotnet run --project src/Sigov.Worker --no-build -c Release >>"$ARTIFACT_DIR/worker-$STAMP.log" 2>&1 & PIDS+=("$!")
fi

probe() {
  local kind="$1" base="$2" path="$3" allowed="$4" code
  code="$(curl -kSs --max-time 15 -o /dev/null -w '%{http_code}' "$base$path" || printf 000)"
  if [[ ",$allowed," == *",$code,"* ]] && [[ "$code" != 404 && "$code" != 500 && "$code" != 501 ]]; then
    record "$kind" PASS "$path HTTP $code"
  else
    record "$kind" FAIL "$path HTTP $code (esperado $allowed)"; FAILED=1
  fi
}

for attempt in {1..40}; do
  code="$(curl -kSs --max-time 2 -o /dev/null -w '%{http_code}' "$API_URL/health" || true)"
  [[ "$code" != 000 && -n "$code" ]] && break
  sleep 1
done

while IFS='|' read -r kind target path allowed; do
  [[ -z "$path" || "$path" == \#* ]] && continue
  [[ "$target" == WEB ]] && probe "$kind" "$WEB_URL" "$path" "$allowed" || probe "$kind" "$API_URL" "$path" "$allowed"
done < <(python3 - <<'PY'
import json
with open('scripts/homologacao/homologacao-funcional-http.json', encoding='utf-8') as source:
    for item in json.load(source)['probes']:
        print('|'.join((item['group'], item['target'], item['path'], ','.join(map(str,item['allowed'])))))
PY
)

record permissions INFO "jornadas autenticadas requerem SIGOV_HOMOLOGACAO_TOKEN_*; probes anonimos confirmam 302/401/403"
record result "$([[ $FAILED == 0 ]] && echo PASS || echo FAIL)" "artifact sanitizado; nenhum cookie, token ou senha gravado"
