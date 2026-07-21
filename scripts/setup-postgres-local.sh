#!/usr/bin/env bash
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
: "${SIGOV_DB_HOST:=localhost}"; : "${SIGOV_DB_PORT:=5432}"; : "${SIGOV_DB_NAME:=sigov}"; : "${SIGOV_DB_USER:=sigov}"
command -v psql >/dev/null
psql -v ON_ERROR_STOP=1 -h "$SIGOV_DB_HOST" -p "$SIGOV_DB_PORT" -U "$SIGOV_DB_USER" -d "$SIGOV_DB_NAME" -f "$ROOT/script_completop.sql"
psql -v ON_ERROR_STOP=1 -h "$SIGOV_DB_HOST" -p "$SIGOV_DB_PORT" -U "$SIGOV_DB_USER" -d "$SIGOV_DB_NAME" -c 'select count(*) from sigov.schema_migrations' >/dev/null
