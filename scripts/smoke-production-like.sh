#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$ROOT"
out="artifacts/smoke/rc50_52_prod_smoke_result.txt"; mkdir -p "$(dirname "$out")"; : > "$out"
log(){ printf '%s\n' "$*" | tee -a "$out"; }
run(){ log "RUN $*"; "$@" >>"$out" 2>&1; log "PASS $*"; }
run python -m json.tool database/postgres/migrations/manifest.json
run ./scripts/check-migration-partial-index-columns.sh database/postgres/migrations
run ./scripts/check-migration-index-columns.sh database/postgres/migrations
run ./scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations
run bash scripts/check-api-route-conflicts.sh
if command -v psql >/dev/null && [[ "${SIGOV_SMOKE_APPLY_DATABASE:-false}" == true ]]; then
  export PGPASSWORD="${PGPASSWORD:?Defina PGPASSWORD somente no ambiente}"
  run psql --host "${SIGOV_DB_HOST:-localhost}" --port "${SIGOV_DB_PORT:-5432}" --username "${SIGOV_DB_USER:-postgres}" --dbname "${SIGOV_DB_NAME:-postgres}" --set ON_ERROR_STOP=1 --file database/postgres/script_completo_dev.sql
else log 'SKIP database (psql ausente ou SIGOV_SMOKE_APPLY_DATABASE != true)'; fi
if command -v dotnet >/dev/null; then run dotnet restore sigov.runtime.slnf --locked-mode; run dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror; else log 'SKIP build (dotnet ausente)'; fi
probe(){ local name=$1 url=$2 expected=${3:-200}; local code; code="$(curl --silent --show-error --insecure --output /dev/null --write-out '%{http_code}' --max-time 15 "$url")"; [[ "$code" == "$expected" ]] || { log "FAIL $name HTTP $code (esperado $expected)"; return 1; }; log "PASS $name HTTP $code"; }
if [[ -n "${SIGOV_API_BASE_URL:-}" ]]; then probe api-health "$SIGOV_API_BASE_URL/api/observabilidade/health"; [[ "${SIGOV_SWAGGER_ENABLED:-false}" != true ]] || probe swagger "$SIGOV_API_BASE_URL/swagger/v1/swagger.json"; else log 'SKIP API probes (SIGOV_API_BASE_URL ausente)'; fi
if [[ -n "${SIGOV_WEB_BASE_URL:-}" ]]; then
  probe login "$SIGOV_WEB_BASE_URL/Auth/Login"
  for path in MinhaCentral SystemHealth/ProjectStatus Observabilidade/Dashboard Seguranca/Dashboard Auditoria/Dashboard Lgpd/Dashboard; do probe "web-$path" "$SIGOV_WEB_BASE_URL/$path" "${SIGOV_AUTHENTICATED_EXPECTED_STATUS:-302}"; done
else log 'SKIP Web probes (SIGOV_WEB_BASE_URL ausente)'; fi
log 'SMOKE COMPLETO (saída sanitizada: nenhuma senha ou connection string registrada)'
