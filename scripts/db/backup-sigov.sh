#!/usr/bin/env bash
set -Eeuo pipefail
: "${SIGOV_DB_HOST:=localhost}" "${SIGOV_DB_PORT:=5432}" "${SIGOV_DB_NAME:=postgres}" "${SIGOV_DB_USER:=postgres}" "${SIGOV_DB_SCHEMA:=sigov}"
[[ "$SIGOV_DB_SCHEMA" =~ ^[a-z_][a-z0-9_]*$ ]] || { echo "ERRO: SIGOV_DB_SCHEMA inválido." >&2; exit 64; }
command -v pg_dump >/dev/null || { echo "ERRO: pg_dump não encontrado." >&2; exit 127; }
out_dir="${1:-artifacts/backups}"; mkdir -p "$out_dir"
timestamp="$(date -u +%Y%m%dT%H%M%SZ)"; output="$out_dir/sigov_${SIGOV_DB_NAME}_${timestamp}.dump"
pg_dump --host "$SIGOV_DB_HOST" --port "$SIGOV_DB_PORT" --username "$SIGOV_DB_USER" --dbname "$SIGOV_DB_NAME" --schema "$SIGOV_DB_SCHEMA" --format custom --no-owner --no-privileges --file "$output"
printf '%s\n' "$output"
