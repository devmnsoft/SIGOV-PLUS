#!/usr/bin/env bash
set -Eeuo pipefail

: "${POSTGRES_DB:=postgres}"
: "${POSTGRES_USER:=postgres}"
: "${POSTGRES_HOST:=postgres}"
: "${POSTGRES_PORT:=5432}"

echo "Aguardando PostgreSQL..."
until pg_isready -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"; do
  sleep 2
done

echo "PostgreSQL pronto."

psql_base=(
  psql
  -h "${POSTGRES_HOST}"
  -p "${POSTGRES_PORT}"
  -U "${POSTGRES_USER}"
  -d "${POSTGRES_DB}"
  -v ON_ERROR_STOP=1
)

run_sql() {
  "${psql_base[@]}" -c "$1"
}

apply_sql_file() {
  local file="$1"
  local version="$2"
  local checksum
  checksum="$(sha256sum "$file" | awk '{print $1}')"

  if "${psql_base[@]}" -tAc "select 1 from sigov.docker_schema_migrations where version = '${version//\'/''}' and checksum = '${checksum//\'/''}' limit 1;" | grep -q 1; then
    echo "Migration já aplicada: ${version} (${file})"
    return 0
  fi

  local existing_checksum
  existing_checksum="$("${psql_base[@]}" -tAc "select checksum from sigov.docker_schema_migrations where version = '${version//\'/''}' limit 1;" | tr -d '[:space:]')"
  if [[ -n "${existing_checksum}" && "${existing_checksum}" != "${checksum}" ]]; then
    echo "ERRO: migration ${version} já foi aplicada com checksum diferente." >&2
    exit 1
  fi

  echo "Aplicando migration: ${version} (${file})"
  "${psql_base[@]}" --single-transaction -f "$file"
  "${psql_base[@]}" -c "insert into sigov.docker_schema_migrations (version, file_path, checksum) values ('${version//\'/''}', '${file//\'/''}', '${checksum//\'/''}') on conflict (version) do nothing;"
}

if [ -f /database/apply_all_required_migrations.sql ]; then
  echo "Aplicando /database/apply_all_required_migrations.sql..."
  "${psql_base[@]}" --single-transaction -f /database/apply_all_required_migrations.sql
else
  echo "Arquivo /database/apply_all_required_migrations.sql não encontrado." >&2
  exit 1
fi

run_sql "create schema if not exists sigov;"
run_sql "create table if not exists sigov.docker_schema_migrations (version varchar(250) primary key, file_path text not null, checksum varchar(128) not null, applied_at timestamptz not null default now());"

shopt -s nullglob
for file in /database/postgres/migrations/*.sql /database/migrations/*.sql; do
  version="$(basename "$file" .sql)"
  apply_sql_file "$file" "$version"
done

echo "Migrations aplicadas com sucesso."
