#!/usr/bin/env bash
set -uo pipefail
api="${SIGOV_API_BASE_URL:-http://localhost:5001}"
base="${SIGOV_WEB_BASE_URL:-${SIGOV_WEB_URL:-http://localhost:5002}}"
output="${1:-artifacts/smoke/rc50_54_critical_pages_result.txt}"
mkdir -p "$(dirname "$output")"; : > "$output"
failed=0
for path in /health /api/observabilidade/health /api/observabilidade/liveness /swagger/v1/swagger.json; do
  code="$(curl -ksS -o /dev/null -w '%{http_code}' "$api$path" || true)"
  case "$code" in 200|302|401|403) status=OK;; *) status=FAIL; failed=1;; esac
  printf '%s API %s HTTP %s\n' "$status" "$path" "$code" | tee -a "$output"
done
for path in /Auth/Login /MinhaCentral /SystemHealth/ProjectStatus /Observabilidade/Dashboard /Seguranca/Dashboard /Auditoria/Dashboard /Lgpd/Dashboard /Tributario/Dashboard /Educacao/Dashboard /Saude/Dashboard /Saneamento/Dashboard; do
  code="$(curl -ksS -o /dev/null -w '%{http_code}' "$base$path" || true)"
  case "$code" in 200|302|401|403) status=OK;; *) status=FAIL; failed=1;; esac
  printf '%s %s HTTP %s\n' "$status" "$path" "$code" | tee -a "$output"
done
exit "$failed"
