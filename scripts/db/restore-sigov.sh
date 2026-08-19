#!/usr/bin/env bash
set -Eeuo pipefail
[[ $# -eq 1 ]] || { echo "Uso: $0 BACKUP.dump" >&2; exit 64; }
: "${SIGOV_DB_HOST:=localhost}" "${SIGOV_DB_PORT:=5432}" "${SIGOV_DB_NAME:?Defina SIGOV_DB_NAME com um banco separado de restore}" "${SIGOV_DB_USER:=postgres}" "${SIGOV_DB_SCHEMA:=sigov}"
[[ "$SIGOV_DB_SCHEMA" =~ ^[a-z_][a-z0-9_]*$ ]] || { echo "ERRO: SIGOV_DB_SCHEMA inválido." >&2; exit 64; }
[[ -f "$1" ]] || { echo "ERRO: backup inexistente: $1" >&2; exit 66; }
[[ "$SIGOV_DB_NAME" != "postgres" ]] || { echo "ERRO: restore operacional deve usar banco separado, nunca postgres." >&2; exit 64; }
command -v pg_restore >/dev/null || { echo "ERRO: pg_restore não encontrado." >&2; exit 127; }
pg_restore --host "$SIGOV_DB_HOST" --port "$SIGOV_DB_PORT" --username "$SIGOV_DB_USER" --dbname "$SIGOV_DB_NAME" --no-owner --no-privileges --exit-on-error "$1"
