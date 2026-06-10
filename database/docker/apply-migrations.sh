#!/usr/bin/env bash
set -Eeuo pipefail

echo "Aguardando PostgreSQL..."

until pg_isready -h postgres -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"; do
  sleep 2
done

echo "PostgreSQL pronto."

psql_base=(psql -h postgres -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v ON_ERROR_STOP=1)

if [ -f /database/apply_all_required_migrations.sql ]; then
  echo "Aplicando /database/apply_all_required_migrations.sql..."
  "${psql_base[@]}" -f /database/apply_all_required_migrations.sql
else
  echo "Arquivo /database/apply_all_required_migrations.sql não encontrado."
  exit 1
fi

if [ -d /database/postgres/migrations ]; then
  echo "Aplicando migrations versionadas de /database/postgres/migrations..."
  while IFS= read -r migration; do
    file_path="${migration#/database/}"
    version="$(basename "${migration}" .sql)"
    checksum="$(sha256sum "${migration}" | awk '{print $1}')"
    already_applied="$("${psql_base[@]}" -Atc "select exists(select 1 from sigov.docker_schema_migrations where version='${version//\'/''}');")"
    if [ "${already_applied}" = "t" ]; then
      echo "Migration ${version} já aplicada."
      continue
    fi

    echo "Aplicando migration ${version}..."
    "${psql_base[@]}" -f "${migration}"
    "${psql_base[@]}" -c "insert into sigov.docker_schema_migrations(version,file_path,checksum) values ('${version//\'/''}','${file_path//\'/''}','${checksum//\'/''}') on conflict(version) do nothing;"
  done < <(find /database/postgres/migrations -maxdepth 1 -type f -name '*.sql' | sort)
fi

echo "Migrations aplicadas com sucesso."
