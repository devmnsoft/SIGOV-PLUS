#!/usr/bin/env bash
set -uo pipefail
base="${SIGOV_WEB_URL:-https://localhost:7002}"
output="${1:-artifacts/smoke/rc50_52_pages_result.txt}"
mkdir -p "$(dirname "$output")"; : > "$output"
failed=0
for path in /Auth/Login /MinhaCentral /SystemHealth/ProjectStatus /Observabilidade/Dashboard /Seguranca/Dashboard /Auditoria/Dashboard /Lgpd/Dashboard; do
  code="$(curl -ksS -o /dev/null -w '%{http_code}' "$base$path" || true)"
  case "$code" in 200|302|401|403) status=OK;; *) status=FAIL; failed=1;; esac
  printf '%s %s HTTP %s\n' "$status" "$path" "$code" | tee -a "$output"
done
exit "$failed"
