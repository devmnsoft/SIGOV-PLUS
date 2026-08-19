#!/usr/bin/env bash
set -Eeuo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$ROOT"
out="${SIGOV_SMOKE_OUTPUT:-artifacts/smoke/rc50_53_prod_smoke_result.txt}"
mkdir -p "$(dirname "$out")"; : > "$out"
log(){ printf '%s\n' "$*" | tee -a "$out"; }
blocked=0
run(){
  local name=$1; shift
  local started=$SECONDS exit_code
  log "RUN $name"
  set +e; "$@" >>"$out" 2>&1; exit_code=$?; set -e
  log "$([[ $exit_code -eq 0 ]] && echo PASS || echo FAIL) $name exit_code=$exit_code duration_seconds=$((SECONDS-started))"
  return "$exit_code"
}
run manifest python -m json.tool database/postgres/migrations/manifest.json
run partial-indexes ./scripts/check-migration-partial-index-columns.sh database/postgres/migrations
run indexes ./scripts/check-migration-index-columns.sh database/postgres/migrations
run immutable-indexes ./scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations
run route-conflicts bash scripts/check-api-route-conflicts.sh
if ! command -v psql >/dev/null; then
  log 'SKIP database reason=psql_not_found classification=P0_ENVIRONMENTAL'
  blocked=1
elif [[ "${SIGOV_SMOKE_APPLY_DATABASE:-false}" != true ]]; then
  log 'SKIP database reason=SIGOV_SMOKE_APPLY_DATABASE_not_true'
else
  export PGPASSWORD="${PGPASSWORD:?Defina PGPASSWORD somente no ambiente}"
  run database psql --host "${SIGOV_DB_HOST:-localhost}" --port "${SIGOV_DB_PORT:-5432}" --username "${SIGOV_DB_USER:-postgres}" --dbname "${SIGOV_DB_NAME:-postgres}" --set ON_ERROR_STOP=1 --file database/postgres/script_completo_dev.sql
fi
if command -v dotnet >/dev/null; then
  run restore dotnet restore sigov.runtime.slnf --locked-mode
  run build dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror
else
  log 'SKIP build reason=dotnet_not_found classification=P0_ENVIRONMENTAL'
  blocked=1
fi
probe(){
  local name=$1 url=$2 expected=${3:-200} started=$SECONDS code exit_code=0
  code="$(curl --silent --show-error --insecure --output /dev/null --write-out '%{http_code}' --max-time 15 "$url")" || exit_code=$?
  if [[ $exit_code -ne 0 || "$code" != "$expected" ]]; then
    log "FAIL $name endpoint=$url http_status=$code expected=$expected exit_code=$exit_code duration_seconds=$((SECONDS-started))"; return 1
  fi
  log "PASS $name endpoint=$url http_status=$code exit_code=0 duration_seconds=$((SECONDS-started))"
}
if [[ -n "${SIGOV_API_BASE_URL:-}" ]]; then
  probe api-health "$SIGOV_API_BASE_URL/api/observabilidade/health"
  [[ "${SIGOV_SWAGGER_ENABLED:-false}" != true ]] || probe swagger "$SIGOV_API_BASE_URL/swagger/v1/swagger.json"
else log 'SKIP API probes reason=SIGOV_API_BASE_URL_not_set'; fi
if [[ -n "${SIGOV_WEB_BASE_URL:-}" ]]; then
  probe login "$SIGOV_WEB_BASE_URL/Auth/Login"
  for path in MinhaCentral SystemHealth/ProjectStatus Observabilidade/Dashboard Seguranca/Dashboard Seguranca/Permissoes Auditoria/Dashboard Lgpd/Dashboard; do
    probe "web-$path" "$SIGOV_WEB_BASE_URL/$path" "${SIGOV_AUTHENTICATED_EXPECTED_STATUS:-302}"
  done
else log 'SKIP Web probes reason=SIGOV_WEB_BASE_URL_not_set'; fi
log 'SMOKE COMPLETE output=sanitized secrets=not_logged'
if [[ $blocked -ne 0 ]]; then
  log 'GATE BLOCKED reason=mandatory_tooling_unavailable exit_code=2'
  exit 2
fi
