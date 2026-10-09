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
# RC-EVO-B §6: a reconciliação é bidirecional na Fase 2 — toda migration que o
# sidecar aplica (ou já aplicou) é registrada no ledger canônico do app
# (sigov.schema_migrations, source='docker-sidecar'); divergência de checksum
# entre manifest, ledger do sidecar e ledger canônico falha explicitamente
# (regra 13), sem sobrescrever estado divergente em silêncio.
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

  # Mapa file -> version/checksum/description/category/postConditionSql/
  # knownChecksums do manifest (fonte de autoridade da chave, do checksum e dos
  # metadados de ledger). Colunas separadas por TAB; o manifest nao contem TAB
  # nem aspas escapadas nos valores, entao a extracao ingenua e segura
  # (validada pelo gate de integridade abaixo e pela comparacao de checksum por
  # arquivo). knownChecksums (lista separada por espaco) sao os hashes
  # historicos aceitos em ledger para arquivos republicados (regra 8: a
  # republicacao vem acompanhada de migration/postcondicao corretiva).
  # Nota: entradas com postConditionProbes tem o fechamento aninhado antecipado
  # e perdem apenas o postConditionSql no mapa; essas versoes ja estao no
  # acervo (so passam pelo caminho de reconciliacao, que nao usa pos-condicao).
  version_map="$(awk '
    /^      "version"[[:space:]]*:[[:space:]]*"/     { line = $0; sub(/^ *"version"[[:space:]]*:[[:space:]]*"/, "", line);    sub(/".*/, "", line);    v = line }
    /^      "description"[[:space:]]*:[[:space:]]*"/ { line = $0; sub(/^ *"description"[[:space:]]*:[[:space:]]*"/, "", line); sub(/".*/, "", line);    d = line }
    /^      "category"[[:space:]]*:[[:space:]]*"/    { line = $0; sub(/^ *"category"[[:space:]]*:[[:space:]]*"/, "", line);    sub(/".*/, "", line);    c = line }
    /^      "file"[[:space:]]*:[[:space:]]*"/        { line = $0; sub(/^ *"file"[[:space:]]*:[[:space:]]*"/, "", line);        sub(/".*/, "", line);    f = line }
    /^      "checksum"[[:space:]]*:[[:space:]]*"/    { line = $0; sub(/^ *"checksum"[[:space:]]*:[[:space:]]*"/, "", line);    sub(/".*/, "", line);    k = line }
    /^      "postConditionSql"[[:space:]]*:[[:space:]]*"/ { line = $0; sub(/^ *"postConditionSql"[[:space:]]*:[[:space:]]*"/, "", line); sub(/".*/, "", line); p = line }
    /^      "knownChecksums"[[:space:]]*:[[:space:]]*\[/ { ink = 1 }
    /^[[:space:]]+\],?[[:space:]]*$/                 { ink = 0 }
    /^[[:space:]]*\},?[[:space:]]*$/                 { ink = 0; if (v != "" && f != "") { print f "\t" v "\t" k "\t" d "\t" c "\t" p "\t" kn }; v = ""; f = ""; k = ""; d = ""; c = ""; p = ""; kn = "" }
    ink == 1 && /^[[:space:]]+"[0-9a-f][0-9a-f]*",?[[:space:]]*$/ { h = $0; gsub(/[^0-9a-f]/, "", h); if (h != "") kn = kn (kn == "" ? "" : " ") h }
  ' "${manifest_file}")"

  # Gate de integridade do parse: o numero de pares file->version deve ser
  # exatamente igual ao numero de linhas "version" presentes no manifest.
  # Divergencia indica formato inesperado; falha explicita em vez de pular.
  manifest_version_total="$(grep -c '"version"[[:space:]]*:' "${manifest_file}")"
  manifest_version_anchored="$(grep -c '^      "version"[[:space:]]*:' "${manifest_file}" || true)"
  #Checksum no mapa: o hash so aparece como coluna propia (TAB + 64 hex + TAB);
  #o grep ancorado no manifest contaria tambem o checksum dos blocos
  #compatibilityAfterAll (mesma indentacao de 6 espacos).
  manifest_checksum_in_map="$(printf '%s\n' "${version_map}" | grep -c "$(printf '\t')[0-9a-f]\{64\}$(printf '\t')" || true)"
  manifest_map_rows="$(printf '%s\n' "${version_map}" | grep -c . || true)"
  if [ "${manifest_version_anchored}" -ne "${manifest_version_total}" ] || [ "${manifest_map_rows}" -ne "${manifest_version_total}" ] || [ "${manifest_checksum_in_map}" -ne "${manifest_version_total}" ]; then
    echo "Falha no parse do manifest.json: linhas 'version' totais=${manifest_version_total}, ancoradas=${manifest_version_anchored}, checksums no mapa=${manifest_checksum_in_map}, pares file->version=${manifest_map_rows}."
    exit 1
  fi
fi

# checksum_normalizado: mesmo calculo do applier oficial do manifest (conteudo
# UTF-8 sem BOM e com CRLF normalizado para LF). Arquivos do acervo ja sao LF,
# entao o resultado coincide com sha256sum cru; a normalizacao protege contra
# checkout que converta finais de linha.
checksum_normalizado() {
  sed -e '1s/^\xEF\xBB\xBF//' -e 's/\r$//' "$1" | sha256sum | awk '{print $1}'
}

# reconcile_ledger_canonico: RC-EVO-B §6. Registro a presenca no ledger do
# sidecar como prova de aplicacao e grava/resguarda a linha no ledger canonico
# do app. Checksum aceito em ledger = checksum publicado OU um dos
# knownChecksums da entrada (republicacao historica, regra 8); qualquer outro
# valor falha com nome proprio (DIVERGENCIA_*), sem sobrescrever em silencio
# (regra 13). A linha canonica recebe o checksum de procedencia do sidecar
# (o que foi efetivamente aplicado), nunca uma substituicao silenciosa.
reconcile_ledger_canonico() {
  local version="$1" checksum="$2" description="$3" category="$4" permitidos="$5"
  "${psql_base[@]}" -q <<SQL
do \$reconcile_evo_b\$
declare
    v_ck_sidecar text;
    v_ck_canonico text;
    v_registra text;
begin
    select lower(checksum) into v_ck_sidecar from sigov.docker_schema_migrations where version = '${version//\'/''}';
    select lower(checksum) into v_ck_canonico from sigov.schema_migrations where version = '${version//\'/''}';
    if coalesce(v_ck_sidecar, '') <> '' and v_ck_sidecar not in (${permitidos}) then
        raise exception 'DIVERGENCIA_LEDGER_SIDECAR: versao % tem checksum % no sidecar, que nao e o publicado nem um knownChecksum do manifest; reconcilie antes de prosseguir',
            '${version//\'/''}', v_ck_sidecar;
    end if;
    if coalesce(v_ck_canonico, '') <> '' and v_ck_canonico not in (${permitidos}) then
        raise exception 'DIVERGENCIA_LEDGER_CANONICO: versao % tem checksum % no ledger do app, que nao e o publicado nem um knownChecksum do manifest; reconcilie antes de prosseguir',
            '${version//\'/''}', v_ck_canonico;
    end if;
    v_registra := coalesce(nullif(v_ck_sidecar, ''), '${checksum//\'/''}');
    insert into sigov.schema_migrations (version, description, checksum, category, source, success, applied_at)
    values ('${version//\'/''}', '${description//\'/''}', v_registra, '${category//\'/''}', 'docker-sidecar', true,
            coalesce((select applied_at from sigov.docker_schema_migrations where version = '${version//\'/''}'), now()))
    on conflict (version) do nothing;
end
\$reconcile_evo_b\$;
SQL
}

if [ -d "${migrations_dir}" ]; then
  echo "Aplicando migrations versionadas de /database/postgres/migrations..."
  while IFS= read -r migration; do
    base_file="$(basename "${migration}")"
    file_path="${migration#/database/}"
    map_row="$(printf '%s\n' "${version_map}" | awk -F '\t' -v f="${base_file}" '$1 == f { for (i = 2; i <= NF; i++) printf "%s%s", $i, (i < NF ? "\t" : ""); exit }')"
    if [ -z "${map_row}" ]; then
      # Fora do manifest (historicas geridas por outros fluxos): o ManifestRunner
      # do app tambem as ignora; pulamos de forma explicita sem aplicar.
      echo "Migration ${base_file} ausente no manifest; pulando (fora da autoridade do manifest)."
      continue
    fi
    IFS=$'\t' read -r version manifest_checksum manifest_description manifest_category manifest_post manifest_known <<<"$(printf '%s\tX\tX' "${map_row}")"
    permitidos="'${manifest_checksum//\'/''}'"
    for known_hash in ${manifest_known}; do
      permitidos="${permitidos},'${known_hash}'"
    done
    if printf '%s\n' "${excluded_versions}" | grep -qx -- "${version}"; then
      echo "Migration ${version} com applyAutomatically=false; pulando (autoridade do manifest)."
      continue
    fi
    checksum="$(checksum_normalizado "${migration}")"
    if [ "${checksum}" != "${manifest_checksum}" ]; then
      echo "DIVERGENCIA_CHECKSUM_ARQUIVO: ${version} (${base_file}) tem checksum ${checksum} e o manifest determina ${manifest_checksum}; o arquivo alterado nao corresponde ao publicado."
      exit 1
    fi
    already_applied="$("${psql_base[@]}" -Atc "select exists(select 1 from sigov.docker_schema_migrations where version='${version//\'/''}');")"
    if [ "${already_applied}" = "t" ]; then
      # Ja aplicada pelo sidecar em execucao anterior: reconcilia o ledger
      # canonico (fecha o gap historico docker_schema_migrations ->
      # schema_migrations) em vez de apenas pular.
      reconcile_ledger_canonico "${version}" "${checksum}" "${manifest_description}" "${manifest_category}" "${permitidos}"
      echo "Migration ${version} já aplicada; ledger canonico reconciliado."
      continue
    fi

    echo "Aplicando migration ${version}..."
    "${psql_base[@]}" -f "${migration}"
    if [ -n "${manifest_post}" ]; then
      "${psql_base[@]}" -q -c "do \$sigov_post\$ begin if not (${manifest_post}) then raise exception 'postConditionSql reprovada para ${version}'; end if; end \$sigov_post\$;"
    fi
    "${psql_base[@]}" -c "insert into sigov.docker_schema_migrations(version,file_path,checksum) values ('${version//\'/''}','${file_path//\'/''}','${checksum//\'/''}') on conflict(version) do nothing;"
    reconcile_ledger_canonico "${version}" "${checksum}" "${manifest_description}" "${manifest_category}" "${permitidos}"
  done < <(find "${migrations_dir}" -maxdepth 1 -type f -name '*.sql' | sort)
fi

# Guarda final de divergencia (regra 13): qualquer versao presente nos dois
# ledgers com checksums diferentes falha o fluxo; marker rows (version NULL) e
# checksums ausentes nao sao julgados.
"${psql_base[@]}" -q <<'SQL'
do $reconcile_final$
declare
    v_divergencias text;
begin
    select string_agg(format('%s (sidecar=%s, canonico=%s)', d.version, d.checksum, c.checksum), '; ' order by d.version)
      into v_divergencias
      from sigov.docker_schema_migrations d
      join sigov.schema_migrations c on c.version = d.version
     where d.version is not null
       and d.checksum is not null and c.checksum is not null
       and lower(d.checksum) <> lower(c.checksum);
    if v_divergencias is not null then
        raise exception 'DIVERGENCIA_LEDGER_LADO_A_LADO: %', v_divergencias;
    end if;
end
$reconcile_final$;
SQL

echo "Migrations aplicadas com sucesso; ledgers do sidecar e do app reconciliados."
