#!/usr/bin/env bash
set -uo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"; cd "$root"
out=artifacts/smoke/rc50_52_smoke_result.txt; mkdir -p "$(dirname "$out")"; : > "$out"; failed=0
run(){ printf '\n> %s\n' "$*" >>"$out"; "$@" >>"$out" 2>&1 || { printf 'FAIL (exit %s)\n' "$?" >>"$out"; failed=1; }; }
run python3 -m json.tool database/postgres/migrations/manifest.json
run bash scripts/check-migration-partial-index-columns.sh database/postgres/migrations
run bash scripts/check-migration-index-columns.sh database/postgres/migrations
run bash scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations
if command -v psql >/dev/null; then PGPASSWORD="${SIGOV_DB_PASSWORD:-}" run psql -h "${SIGOV_DB_HOST:-localhost}" -p "${SIGOV_DB_PORT:-5432}" -U "${SIGOV_DB_USER:-postgres}" -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql; else echo 'SKIP psql indisponível' >>"$out"; failed=1; fi
if command -v dotnet >/dev/null; then run dotnet restore sigov.runtime.slnf --locked-mode; run dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror; else echo 'SKIP dotnet indisponível' >>"$out"; failed=1; fi
if command -v curl >/dev/null; then run curl -ksSf "${SIGOV_API_URL:-https://localhost:7001}/swagger/v1/swagger.json"; run curl -ksSf "${SIGOV_API_URL:-https://localhost:7001}/health"; run bash scripts/check-critical-pages.sh; fi
printf '\nResultado: %s\n' "$([ "$failed" -eq 0 ] && echo APROVADO || echo REPROVADO)" >>"$out"; cat "$out"; exit "$failed"
