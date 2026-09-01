#!/usr/bin/env bash
set -euo pipefail

: "${PGHOST:=localhost}" "${PGPORT:=5432}" "${PGUSER:=postgres}" "${PGDATABASE:=sigov_plus}"
readonly BASE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/SIGOV_PLUS_BASE_COMPLETA_RESTAURAVEL.sql"
command -v psql >/dev/null 2>&1 || { echo "BLOCKED: comando psql não executado porque psql não está instalado ou não está no PATH." >&2; exit 127; }
psql -X -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" -f "$BASE"
psql -X -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$PGDATABASE" <<'SQL'
select count(*) as tabelas from information_schema.tables where table_schema='sigov';
select count(*) as modulos from sigov.modulo_saas where ativo and not is_deleted;
select count(*) as permissoes from sigov.permissao where ativo and not is_deleted;
select count(*) as super_admins from sigov.usuario where email='superadmin@mnsoft.local' and ativo and not is_deleted;
SQL
