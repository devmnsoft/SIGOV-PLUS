#!/usr/bin/env bash
set -Eeuo pipefail

# Host do PostgreSQL na rede do compose. SIGOV_PG_HOST permite executar o script
# fora da rede (ex.: teste local apontando para localhost).
PG_HOST="${SIGOV_PG_HOST:-postgres}"

echo "Aguardando PostgreSQL..."

until pg_isready -h "${PG_HOST}" -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}"; do
  sleep 2
done

echo "PostgreSQL pronto."

psql_base=(psql -h "${PG_HOST}" -p 5432 -U "${POSTGRES_USER}" -d "${POSTGRES_DB}" -v ON_ERROR_STOP=1)

baseline_marker="00000000000000_script_completo_baseline"

# Fase 1: baseline (script completo) roda somente em volumes novos. Em volumes
# existentes (marker presente) o re-run do script completo é pulado: a base já
# evoluiu via migrations versionadas e o estado canônico segue o ledger do app
# (sigov.schema_migrations), que a validação do MigrationRunner audita no startup.
baseline_applied="$("${psql_base[@]}" -Atc "select case when to_regclass('sigov.docker_schema_migrations') is null then 'no' else case when exists(select 1 from sigov.docker_schema_migrations where name='${baseline_marker}') then 'yes' else 'no' end end;" || echo 'no')"

if [ "${baseline_applied}" = "yes" ]; then
  echo "Baseline ja aplicado neste volume; pulando reexecucao do script completo."
else
  if [ -f /database/apply_all_required_migrations.sql ]; then
    echo "Aplicando /database/apply_all_required_migrations.sql..."
    "${psql_base[@]}" -f /database/apply_all_required_migrations.sql
  else
    echo "Arquivo /database/apply_all_required_migrations.sql não encontrado."
    exit 1
  fi
fi

# Sincronização do ledger do sidecar: normaliza o shape histórico (name) para o
# shape usado pela fase 2 (version/file_path/checksum) e replica as versões já
# registradas no ledger canônico do app, evitando re-aplicar o acervo inteiro.
echo "Sincronizando ledger do sidecar (sigov.docker_schema_migrations)..."
"${psql_base[@]}" <<'SQL'
create table if not exists sigov.docker_schema_migrations (
    id bigint generated always as identity primary key,
    name text unique,
    applied_at timestamptz not null default now()
);
alter table sigov.docker_schema_migrations add column if not exists version text;
alter table sigov.docker_schema_migrations add column if not exists file_path text;
alter table sigov.docker_schema_migrations add column if not exists checksum text;
-- O shape legado criou name como NOT NULL; as linhas de ledger por versao
-- nao possuem name. Unique(name) tolera multiplos NULLs.
alter table sigov.docker_schema_migrations alter column name drop not null;
do $$
begin
    -- Unique full em version (NULLs coexist; marker rows tem version NULL).
    -- Partial index não satisfaz ON CONFLICT (version) sem predicado inferível.
    drop index if exists sigov.uk_sigov_docker_schema_migrations_version;
    create unique index uk_sigov_docker_schema_migrations_version on sigov.docker_schema_migrations (version);
end
$$;
insert into sigov.docker_schema_migrations (version, file_path, checksum, applied_at)
select m.version, m.description || '.sql', m.checksum, m.applied_at
from sigov.schema_migrations m
on conflict (version) do nothing;
SQL

# Fase 2: incrementos versionados. Migrations com applyAutomatically=false no
# manifest (exclusivas/históricas) são puladas, mantendo o manifest como fonte
# de autoridade de execução. Os campos das entradas sao ancorados na indentacao
# de 6 espacos para nao capturar objetos aninhados (compatibilityBefore/After),
# que tambem possuem campos "file" com indentacao de 10 espacos.
migrations_dir="/database/postgres/migrations"
manifest_file="${migrations_dir}/manifest.json"

if [ -d "${migrations_dir}" ] && [ ! -f "${manifest_file}" ]; then
  echo "manifest.json ausente em ${migrations_dir}; abortando para nao aplicar migrations sem autoridade de versao."
  exit 1
fi

excluded_versions=""
version_map=""
manifest_version_total=0
if [ -f "${manifest_file}" ]; then
  excluded_versions="$(awk '
    /^      "version"[[:space:]]*:/ {
      v = $0
      sub(/.*"version"[[:space:]]*:[[:space:]]*"/, "", v)
      sub(/".*/, "", v)
    }
    /"applyAutomatically"[[:space:]]*:[[:space:]]*false/ {
      if (v != "") print v
      v = ""
    }
  ' "${manifest_file}")"

  # Mapa file -> version do manifest (fonte de autoridade da chave de ledger).
  version_map="$(awk '
    /^      "version"[[:space:]]*:/ { line = $0; sub(/^ *"version"[[:space:]]*:[[:space:]]*"/, "", line); sub(/".*/, "", line); v = line }
    /^      "file"[[:space:]]*:/    { line = $0; sub(/^ *"file"[[:space:]]*:[[:space:]]*"/, "", line);    sub(/".*/, "", line); f = line }
    /^[[:space:]]*\},?[[:space:]]*$/ { if (v != "" && f != "") print f "\t" v; v = ""; f = "" }
  ' "${manifest_file}")"

  # Gate de integridade do parse: o numero de pares file->version deve ser
  # exatamente igual ao numero de linhas "version" presentes no manifest.
  # Divergencia indica formato inesperado; falha explicita em vez de pular.
  manifest_version_total="$(grep -c '"version"[[:space:]]*:' "${manifest_file}")"
  manifest_version_anchored="$(grep -c '^      "version"[[:space:]]*:' "${manifest_file}" || true)"
  manifest_map_rows="$(printf '%s\n' "${version_map}" | grep -c . || true)"
  if [ "${manifest_version_anchored}" -ne "${manifest_version_total}" ] || [ "${manifest_map_rows}" -ne "${manifest_version_total}" ]; then
    echo "Falha no parse do manifest.json: linhas 'version' totais=${manifest_version_total}, ancoradas=${manifest_version_anchored}, pares file->version=${manifest_map_rows}."
    exit 1
  fi
fi

if [ -d "${migrations_dir}" ]; then
  echo "Aplicando migrations versionadas de /database/postgres/migrations..."
  while IFS= read -r migration; do
    base_file="$(basename "${migration}")"
    file_path="${migration#/database/}"
    version="$(printf '%s\n' "${version_map}" | awk -F '\t' -v f="${base_file}" '$1 == f { print $2; exit }')"
    if [ -z "${version}" ]; then
      # Fora do manifest (historicas geridas por outros fluxos): o ManifestRunner
      # do app tambem as ignora; pulamos de forma explicita sem aplicar.
      echo "Migration ${base_file} ausente no manifest; pulando (fora da autoridade do manifest)."
      continue
    fi
    if printf '%s\n' "${excluded_versions}" | grep -qx -- "${version}"; then
      echo "Migration ${version} com applyAutomatically=false; pulando (autoridade do manifest)."
      continue
    fi
    checksum="$(sha256sum "${migration}" | awk '{print $1}')"
    already_applied="$("${psql_base[@]}" -Atc "select exists(select 1 from sigov.docker_schema_migrations where version='${version//\'/''}');")"
    if [ "${already_applied}" = "t" ]; then
      echo "Migration ${version} já aplicada."
      continue
    fi

    echo "Aplicando migration ${version}..."
    "${psql_base[@]}" -f "${migration}"
    "${psql_base[@]}" -c "insert into sigov.docker_schema_migrations(version,file_path,checksum) values ('${version//\'/''}','${file_path//\'/''}','${checksum//\'/''}') on conflict(version) do nothing;"
  done < <(find "${migrations_dir}" -maxdepth 1 -type f -name '*.sql' | sort)
fi

echo "Migrations aplicadas com sucesso."
