#!/usr/bin/env bash
set -Eeuo pipefail
: "${SIGOV_DB_HOST:=localhost}" "${SIGOV_DB_PORT:=5432}" "${SIGOV_DB_NAME:?Defina o banco separado restaurado}" "${SIGOV_DB_USER:=postgres}" "${SIGOV_DB_SCHEMA:=sigov}"
[[ "$SIGOV_DB_SCHEMA" =~ ^[a-z_][a-z0-9_]*$ ]] || { echo "ERRO: SIGOV_DB_SCHEMA inválido." >&2; exit 64; }
[[ "$SIGOV_DB_NAME" != "postgres" ]] || { echo "ERRO: verifique o restore em banco separado." >&2; exit 64; }
command -v psql >/dev/null || { echo "ERRO: psql não encontrado." >&2; exit 127; }
psql --host "$SIGOV_DB_HOST" --port "$SIGOV_DB_PORT" --username "$SIGOV_DB_USER" --dbname "$SIGOV_DB_NAME" --set ON_ERROR_STOP=1 --no-psqlrc --tuples-only <<SQL
SELECT CASE WHEN to_regnamespace('${SIGOV_DB_SCHEMA}') IS NOT NULL THEN 'schema_ok' ELSE 'schema_ausente' END;
SELECT count(*) FROM ${SIGOV_DB_SCHEMA}.schema_migrations;
SQL
