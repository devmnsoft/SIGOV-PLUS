#!/usr/bin/env bash
set -uo pipefail
api="${SIGOV_API_BASE_URL:-http://localhost:5001}"
base="${SIGOV_WEB_BASE_URL:-${SIGOV_WEB_URL:-http://localhost:5000}}"
output="${1:-artifacts/smoke/rc50_54_critical_pages_result.txt}"
mkdir -p "$(dirname "$output")"; : > "$output"
failed=0
blocked=0

if ! command -v curl >/dev/null 2>&1; then
  printf '%s\n' 'BLOCKED curl não encontrado; as páginas críticas não foram verificadas.' | tee -a "$output"
  exit 2
fi

check_page() {
  local kind="$1"
  local url="$2"
  local path="$3"
  local code

  code="$(curl -ksS -o /dev/null -w '%{http_code}' "$url$path" 2>/dev/null || true)"
  case "$code" in
    200|302|401|403)
      status=OK
      ;;
    000|'')
      status=BLOCKED
      code="${code:-000}"
      blocked=1
      ;;
    *)
      status=FAIL
      failed=1
      ;;
  esac
  printf '%s%s %s HTTP %s\n' "$status" "$kind" "$path" "$code" | tee -a "$output"
}

for path in /api/health/live /api/observabilidade/health /api/observabilidade/liveness /swagger/v1/swagger.json; do
  check_page ' API' "$api" "$path"
done
for path in /Auth/Login /MinhaCentral /SystemHealth/ProjectStatus /Observabilidade/Dashboard /Seguranca/Dashboard /Auditoria/Dashboard /Lgpd/Dashboard /Tributario/Dashboard /Educacao/Dashboard /Saude/Dashboard /Saneamento/Dashboard; do
  check_page '' "$base" "$path"
done

if (( failed != 0 )); then
  printf '%s\n' 'FAIL: uma ou mais páginas responderam com status HTTP inesperado.' | tee -a "$output"
  exit 1
fi
if (( blocked != 0 )); then
  printf '%s\n' 'BLOCKED: API ou Web indisponível; inicie a aplicação antes de executar o gate.' | tee -a "$output"
  exit 2
fi
printf '%s\n' 'PASS: páginas críticas responderam com status permitido.' | tee -a "$output"
