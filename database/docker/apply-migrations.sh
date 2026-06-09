#!/bin/sh
set -eu

: "${POSTGRES_DB:=postgres}"
: "${POSTGRES_USER:=postgres}"
: "${POSTGRES_HOST:=postgres}"
: "${POSTGRES_PORT:=5432}"

psql_cmd() {
  psql \
    -h "${POSTGRES_HOST}" \
    -p "${POSTGRES_PORT}" \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}" \
    -v ON_ERROR_STOP=1 \
    "$@"
}

sql_escape() {
  printf "%s" "$1" | sed "s/'/''/g"
}

echo "Aguardando PostgreSQL..."
until pg_isready -h "${POSTGRES_HOST}" -p "${POSTGRES_PORT}" -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"; do
  sleep 2
done

echo "PostgreSQL pronto."

if [ -f /database/apply_all_required_migrations.sql ]; then
  echo "Aplicando /database/apply_all_required_migrations.sql..."
  psql_cmd --single-transaction -f /database/apply_all_required_migrations.sql
else
  echo "Arquivo /database/apply_all_required_migrations.sql não encontrado." >&2
  exit 1
fi

psql_cmd -c "create schema if not exists sigov;"
psql_cmd -c "create table if not exists sigov.docker_schema_migrations (version varchar(250) primary key, file_path text not null, checksum varchar(128) not null, applied_at timestamptz not null default now());"

apply_sql_file() {
  file="$1"
  version="$2"
  checksum="$(sha256sum "$file" | awk '{print $1}')"
  version_sql="$(sql_escape "$version")"
  checksum_sql="$(sql_escape "$checksum")"
  file_sql="$(sql_escape "$file")"

  if psql_cmd -tAc "select 1 from sigov.docker_schema_migrations where version = '${version_sql}' and checksum = '${checksum_sql}' limit 1;" | grep -q 1; then
    echo "Migration já aplicada: ${version} (${file})"
    return 0
  fi

  existing_checksum="$(psql_cmd -tAc "select checksum from sigov.docker_schema_migrations where version = '${version_sql}' limit 1;" | tr -d '[:space:]')"
  if [ -n "${existing_checksum}" ] && [ "${existing_checksum}" != "${checksum}" ]; then
    echo "ERRO: migration ${version} já foi aplicada com checksum diferente." >&2
    exit 1
  fi

  echo "Aplicando migration: ${version} (${file})"
  psql_cmd --single-transaction -f "$file"
  psql_cmd -c "insert into sigov.docker_schema_migrations (version, file_path, checksum) values ('${version_sql}', '${file_sql}', '${checksum_sql}') on conflict (version) do nothing;"
}

for dir in /database/postgres/migrations /database/migrations; do
  if [ -d "$dir" ]; then
    find "$dir" -maxdepth 1 -type f -name '*.sql' | sort | while IFS= read -r file; do
      version="$(basename "$file" .sql)"
      apply_sql_file "$file" "$version"
    done
  fi
done

echo "Migrations aplicadas com sucesso."
