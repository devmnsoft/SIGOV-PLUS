#!/usr/bin/env bash
set -Eeuo pipefail

echo "Aguardando PostgreSQL..."

until pg_isready -h postgres -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"; do
  sleep 2
done

echo "PostgreSQL pronto."

if [ -f /database/apply_all_required_migrations.sql ]; then
  echo "Aplicando /database/apply_all_required_migrations.sql..."
  psql \
    -h postgres \
    -p 5432 \
    -U "${POSTGRES_USER}" \
    -d "${POSTGRES_DB}" \
    -v ON_ERROR_STOP=1 \
    -f /database/apply_all_required_migrations.sql
else
  echo "Arquivo /database/apply_all_required_migrations.sql não encontrado."
  exit 1
fi

echo "Migrations aplicadas com sucesso."
