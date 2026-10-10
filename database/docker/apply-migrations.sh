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

migrations_dir="/database/postgres/migrations"
manifest_file="${migrations_dir}/manifest.json"

# Trava advisory oficial do ManifestRunner (chave estavel 0x5349474F56504C55 por
# banco): sidecar e startup nunca executam DDL ao mesmo tempo; quem chega por
# ultimo espera a transacao do outro confirmar e entao reconverge pelos ledgers.
MIGRATION_LOCK="((x'5349474F56504C55')::bit(64)::bigint)"

# Chave propria do sidecar ('SIGOVMIG'), distinta da chave do runner para nao
# deadlockar contra si mesma: o gate de fase (sessao) e a guarda de DDL (transacao)
# convivem sem interferencia.
GATE_LOCK="((x'5349474F564D4947')::bit(64)::bigint)"

tmp_dir="$(mktemp -d)"

# Gate de execucao concorrente (RC-EVO-C §3.1): uma sessao persistente mantem a
# trava de fase enquanto o script trabalha; um segundo sidecar (ou restart do
# compose sobre execucao anterior ainda viva) espera aqui e, ao assumir,
# reconverge pelos ledgers sem reaplicar nada. Se o container morrer, a sessao
# cai e a trava vai junto (sem lock orfao).
# A sessao do gate e identificada por application_name exclusivo da execucao e
# encerrada pelo servidor (pg_terminate_backend): matar por PID de SO arrisca
# alcancar um backend reaproveitado (colisao de PID ja causou crash recovery).
GATE_APP="sigov-docker-migrations-gate-$(date +%s)-$$-${RANDOM}"

# Porteiro: gates com mais de 45 minutos sao de execucoes mortas sem reinicio
# do postmaster (uma execucao completa nao passa de poucos minutos); varre-os
# antes de esperar para o lock nao ficar sequestrado por um gate orfao. O padrao
# de query pega orfaos antigos que nasceram antes do application_name dedicado.
"${psql_base[@]}" -q -Atc "select pg_terminate_backend(pid) from pg_stat_activity where datname = current_database() and (application_name like 'sigov-docker-migrations-gate-%' or (query like 'select pg_advisory_lock%' and query like '%pg_sleep(36000)%')) and pid <> pg_backend_pid() and coalesce(xact_start, query_start) < now() - interval '45 minutes';" >/dev/null 2>&1 || true

# O nome va por set_config DENTRO da sessao: psql moderno envia o proprio
# application_name no startup packet e isso tem prioridade sobre PGOPTIONS.
"${psql_base[@]}" -q -c "select set_config('application_name', '${GATE_APP}', false); select pg_advisory_lock(${GATE_LOCK}); select pg_sleep(36000);" >/dev/null 2>&1 &
gate_pid=$!

cleanup_gate() {
  # Dupla pasada: encerra o portal ja estabelecido e, depois de recolher o
  # cliente, encerra tambem um portal que tenha completado a toma de lock
  # entre as duas etapas (um backend em pg_sleep NAO percebe a morte do
  # cliente e seguraria a trava ate o fim do sono).
  "${psql_base[@]}" -Atc "select pg_terminate_backend(pid) from pg_stat_activity where datname = current_database() and application_name = '${GATE_APP}';" >/dev/null 2>&1 || true
  kill "${gate_pid}" 2>/dev/null || true
  wait "${gate_pid}" 2>/dev/null || true
  "${psql_base[@]}" -Atc "select pg_terminate_backend(pid) from pg_stat_activity where datname = current_database() and application_name = '${GATE_APP}';" >/dev/null 2>&1 || true
  rm -rf "${tmp_dir}"
}
trap cleanup_gate EXIT

gate_waited=0
while :; do
  # So prossegue quando o PORTAL desta execucao segura a trava: um
  # pg_try_advisory_lock generico deixaria o segundo sidecar passar enquanto o
  # portal do primeiro ainda segura a fase (concorrência no staging). O portal
  # do gate faz exatamente uma toma advisory, entao o filtro exclusivo por
  # application_name + datname identifica a cessao sem ambiguidade.
  mine="$("${psql_base[@]}" -Atc "select exists(select 1 from pg_locks l join pg_stat_activity a on a.pid = l.pid where a.datname = current_database() and a.application_name = '${GATE_APP}' and l.locktype = 'advisory' and l.granted);" || echo f)"
  [ "${mine}" = "t" ] && break
  if ! kill -0 "${gate_pid}" 2>/dev/null; then
    echo "GATE_MORTE: a sessao que segurava a trava de fase caiu antes da execucao comecar."
    exit 1
  fi
  gate_waited=$((gate_waited + 1))
  if [ "${gate_waited}" -ge 1800 ]; then
    echo "GATE_TIMEOUT: outro fluxo de migrations nao liberou a trava de fase em 30 minutos."
    exit 1
  fi
  sleep 1
done

# Fase 1: baseline (script completo) roda somente em volumes novos. Em volumes
# existentes (marker presente) o re-run do script completo é pulado: a base já
# evoluiu via migrations versionadas e o estado canônico segue o ledger do app
# (sigov.schema_migrations), que a validação do MigrationRunner audita no startup.
# (a checagem e em duas etapas porque analisar a consulta com FROM na tabela
# ainda inexistente emitiria ERROR no stderr mesmo com o ramo CASE protegido)
baseline_marker_present="no"
if [ "$("${psql_base[@]}" -Atc "select to_regclass('sigov.docker_schema_migrations') is not null;")" = "t" ]; then
  baseline_marker_present="$("${psql_base[@]}" -Atc "select exists(select 1 from sigov.docker_schema_migrations where name='${baseline_marker}');")"
fi

if [ "${baseline_marker_present}" = "t" ]; then
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
"${psql_base[@]}" -q <<'SQL'
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

# ============================================================================
# Fase 2 (RC-EVO-C §3.1): aplicacao incremental com parse ESTRUTURADO do
# manifest.json. O ambiente oficial (imagem postgres:16) nao traz python nem
# jq; o unico parser estruturado garantido no container e o proprio PostgreSQL
# via jsonb. O conteudo do manifest entra no servidor como variavel psql
# (\getenv) e e materializado em sigov.docker_manifest_entries/_probes com
# fidelidade total: arrays aninhados (knownChecksums, postConditionProbes),
# escapes e strings multilinha preservados. Nada e interpretado por indentacao,
# ordem de linhas ou expressao regular sobre o JSON.
#
# Semantica de reconciliacao (regra 13: falha explicita; regra 8: nunca
# sobrescrever checksum publicado):
#   * Aplicar e atomico: DDL + pos-condicoes + registro nos DOIS ledgers dentro
#     de UMA transacao protegida pela trava advisory oficial. Intervencao entre
#     aplicacao e registro deixa de existir (mesmo commit); interrupcao antes do
#     commit nao deixa rastro e o rerun recupera sozinho.
#   * Versao presente em QUALQUER ledger nao e reaplicada (startup-first e
#     sidecar-first convergem sem reexecutar acervo).
#   * Registro novo no ledger canonico so ocorre com success=true depois de
#     comprovar as pos-condicoes aplicaveis da entrada (postConditionSql +
#     todas as postConditionProbes nomeadas). Existir linha no outro ledger
#     nunca basta por si so.
#   * Checksum em ledger so e aceito se igual ao publicado OU listado em
#     knownChecksums (republicacao historica); qualquer outro valor falha com
#     nome proprio (DIVERGENCIA_*) sem sobrescrever nada.
#   * Execucao concorrente de sidecars (ou sidecar x ManifestRunner) e
#     serializada pela mesma trava advisory; o perdedor rever os ledgers e
#     concilia sem reaplicar.
# ============================================================================
if [ -d "${migrations_dir}" ] && [ ! -f "${manifest_file}" ]; then
  echo "manifest.json ausente em ${migrations_dir}; abortando para nao aplicar migrations sem autoridade de versao."
  exit 1
fi

if [ -f "${manifest_file}" ]; then
  echo "Interpretando manifest.json com parser estruturado (jsonb do servidor)..."
  # O manifest entra pelo cliente psql via \copy (montagem /database): variavel
  # de ambiente nao serve (Linux limita cada ENV a 128KiB e o manifest e maior).
  # O formato csv com quote/delimiter/escape em bytes de controle preserva cada
  # linha byte-a-byte (backslash, virgula, aspas e acentos passam crus); a
  # rejuncao por linha ordenada + cast jsonb valida o documento integralmente.
  "${psql_base[@]}" -q <<'SQL'
create schema if not exists sigov;
create temp table manifest_raw (ord int generated always as identity, line text);
-- Path fixo por contrato da imagem (compose monta ./database em /database:ro);
-- mesma constante migrations_dir definida no topo deste script.
\copy manifest_raw (line) from '/database/postgres/migrations/manifest.json' with (format csv, quote E'\x01', delimiter E'\x02', escape E'\x01', null E'\x03')
create temp table manifest_doc as
select string_agg(line, E'\n' order by ord)::jsonb as doc
from manifest_raw;
-- Rejeita JSON malformado antes de qualquer interpretacao (falha nomeada do cast
-- acima, ou documento vazio quando o having nao produz linha).
do $manifest_json$
declare
    v_doc jsonb;
begin
    select doc into v_doc from manifest_doc;
    if v_doc is null then
        raise exception 'MANIFEST_INVALIDO: manifest.json vazio ou ilegivel pelo parser estruturado';
    end if;
    if jsonb_typeof(v_doc -> 'migrations') <> 'array' then
        raise exception 'MANIFEST_INVALIDO: raiz "migrations" ausente ou nao e um array';
    end if;
end
$manifest_json$;

-- Staging do parse (família docker_ledger, reconstruido a cada execucao):
-- autoridade estruturada para todo o restante da fase 2.
drop table if exists sigov.docker_manifest_probes;
drop table if exists sigov.docker_manifest_entries;
create table sigov.docker_manifest_entries (
    version text primary key,
    file_name text not null,
    description text,
    category text,
    checksum text not null,
    known_checksums text[] not null default '{}',
    post_condition_sql text,
    apply_automatically boolean not null default false
);
create table sigov.docker_manifest_probes (
    version text not null references sigov.docker_manifest_entries (version),
    ordinal int not null,
    name text not null,
    sql text not null,
    primary key (version, ordinal)
);
-- Duplicidade verificada ANTES do insert: sem isso a PK da tabela de staging
-- abortaria com erro bruto de unique violation antes de qualquer nome proprio.
do $manifest_dup$
declare
    v_total int;
    v_distintas int;
begin
    select count(*), count(distinct e ->> 'version')
      into v_total, v_distintas
      from jsonb_array_elements((select doc from manifest_doc) -> 'migrations') as e;
    if v_total <> v_distintas then
        raise exception 'MANIFEST_DUPLICADO: versoes repetidas no manifest (% linhas no documento, % distintas)',
            v_total, v_distintas;
    end if;
end
$manifest_dup$;
insert into sigov.docker_manifest_entries
select e ->> 'version',
       e ->> 'file',
       e ->> 'description',
       e ->> 'category',
       e ->> 'checksum',
       coalesce(ka.knowns, '{}'),
       nullif(e ->> 'postConditionSql', ''),
       -- Espelha o ManifestRunner: applyAutomatically so e true quando o JSON
       -- diz true literal; ausente ou outro valor e false (falha fechada).
       coalesce(e ->> 'applyAutomatically', '') = 'true'
from jsonb_array_elements((select doc from manifest_doc) -> 'migrations') as e
-- knownChecksums so e lido quando e array; elementos viram texto escalar (#>>),
-- preservando qualquer valor sem interpretagem adicional.
left join lateral (
    select array_agg(k.item #>> '{}') as knowns
    from jsonb_array_elements(
        case when jsonb_typeof(e -> 'knownChecksums') = 'array'
             then e -> 'knownChecksums' else '[]'::jsonb end
    ) as k(item)
) ka on true;
insert into sigov.docker_manifest_probes
select e ->> 'version', p.ord, p.item ->> 'name', p.item ->> 'sql'
from jsonb_array_elements((select doc from manifest_doc) -> 'migrations') as e
cross join lateral jsonb_array_elements(
    case when jsonb_typeof(e -> 'postConditionProbes') = 'array'
         then e -> 'postConditionProbes' else '[]'::jsonb end
) with ordinality as p(item, ord)
where coalesce(e ->> 'version', '') <> ''
  and jsonb_typeof(p.item) = 'object'
  and coalesce(p.item ->> 'name', '') <> ''
  and coalesce(p.item ->> 'sql', '') <> '';

-- Scripts de compatibilidade (compatibilityBefore por entrada + compatibilityAfterAll
-- na raiz), com os mesmos contratos do ManifestRunner: o arquivo resolve sempre em
-- database/postgres/bootstrap, e apenas nome puro (sem separador de caminho) com
-- checksum hex-64, sem duplicata dentro do mesmo escopo. Semantica espelhada do
-- runner: pre-scripts rodam DENTRO da transacao da entrada quando (e somente
-- quando) ela e aplicada; afterAll roda apos a rodada quando alguma entrada foi
-- aplicada; nenhum dos dois registra em ledger.
drop table if exists sigov.docker_manifest_compat_after;
drop table if exists sigov.docker_manifest_compat_before;
create table sigov.docker_manifest_compat_before (
    version text not null references sigov.docker_manifest_entries (version),
    ordinal int not null,
    file_name text not null,
    checksum text not null,
    primary key (version, ordinal)
);
create table sigov.docker_manifest_compat_after (
    ordinal int primary key,
    file_name text not null,
    checksum text not null
);
do $manifest_compat$
declare
    v_bad text;
    v_dup text;
begin
    select string_agg(format('%s item %s', s.owner_key, s.ord), ', ' order by s.owner_key, s.ord)
      into v_bad
      from (
          select coalesce(e ->> 'version', '<raiz>') as owner_key, c.item, c.ord
          from jsonb_array_elements((select doc from manifest_doc) -> 'migrations') as e
          cross join lateral jsonb_array_elements(
              case when jsonb_typeof(e -> 'compatibilityBefore') = 'array'
                   then e -> 'compatibilityBefore' else '[]'::jsonb end
          ) with ordinality as c(item, ord)
          union all
          select '<raiz>', c.item, c.ord
          from jsonb_array_elements(
              case when jsonb_typeof((select doc from manifest_doc) -> 'compatibilityAfterAll') = 'array'
                   then (select doc from manifest_doc) -> 'compatibilityAfterAll' else '[]'::jsonb end
          ) with ordinality as c(item, ord)
      ) s
     where jsonb_typeof(s.item) <> 'object'
        or coalesce(s.item ->> 'file', '') !~ '^[0-9A-Za-z._-]+$'
        or coalesce(s.item ->> 'checksum', '') !~* '^[0-9a-f]{64}$';
    if v_bad is not null then
        raise exception 'MANIFEST_INVALIDO: itens de compatibilidade com arquivo ou checksum invalido: %', v_bad;
    end if;
    select string_agg(x.owner || '/' || x.f, ', ' order by x.owner, x.f)
      into v_dup
      from (
          select coalesce(e ->> 'version', '?') as owner, c.item ->> 'file' as f
          from jsonb_array_elements((select doc from manifest_doc) -> 'migrations') as e
          cross join lateral jsonb_array_elements(
              case when jsonb_typeof(e -> 'compatibilityBefore') = 'array'
                   then e -> 'compatibilityBefore' else '[]'::jsonb end
          ) as c(item)
          group by 1, 2 having count(*) > 1
          union all
          select '<raiz>' as owner, c.item ->> 'file' as f
          from jsonb_array_elements(
              case when jsonb_typeof((select doc from manifest_doc) -> 'compatibilityAfterAll') = 'array'
                   then (select doc from manifest_doc) -> 'compatibilityAfterAll' else '[]'::jsonb end
          ) as c(item)
          group by 1, 2 having count(*) > 1
      ) x;
    if v_dup is not null then
        raise exception 'MANIFEST_INVALIDO: arquivo de compatibilidade duplicado no mesmo escopo: %', v_dup;
    end if;
end
$manifest_compat$;
insert into sigov.docker_manifest_compat_before
select e ->> 'version', c.ord, c.item ->> 'file', lower(c.item ->> 'checksum')
from jsonb_array_elements((select doc from manifest_doc) -> 'migrations') as e
cross join lateral jsonb_array_elements(
    case when jsonb_typeof(e -> 'compatibilityBefore') = 'array'
         then e -> 'compatibilityBefore' else '[]'::jsonb end
) with ordinality as c(item, ord);
insert into sigov.docker_manifest_compat_after
select c.ord, c.item ->> 'file', lower(c.item ->> 'checksum')
from jsonb_array_elements(
    case when jsonb_typeof((select doc from manifest_doc) -> 'compatibilityAfterAll') = 'array'
         then (select doc from manifest_doc) -> 'compatibilityAfterAll' else '[]'::jsonb end
) with ordinality as c(item, ord);

-- Gate de integridade estrutural (substitui as antigas âncoras de indentação):
-- campos obrigatorios presentes e checksum hex-64 por entrada (a unicidade de
-- versao ja foi verificada antes do insert).
do $manifest_gate$
declare
    v_bad text;
begin
    select string_agg(format('%s (campos obrigatorios ausentes ou checksum invalido)', version), '; ' order by version)
      into v_bad
      from sigov.docker_manifest_entries
     where coalesce(version, '') = ''
        or version !~ '^[0-9A-Za-z._-]+$'
        or coalesce(file_name, '') = ''
        or checksum !~* '^[0-9a-f]{64}$';
    if v_bad is not null then
        raise exception 'MANIFEST_INVALIDO: %', v_bad;
    end if;
end
$manifest_gate$;

-- Familia de funcoes auxiliares do sidecar (mesma classificacao infra do ledger
-- docker; nao sao migrations e nao entram em ledger): confericao de checksums
-- contra a autoridade do manifest e comprovacao de pos-condicoes.
create or replace function sigov.mig_conferir_checksums(p_version text, p_permitidos text[])
returns void language plpgsql as $fn$
declare
    v_ck_sidecar text;
    v_ck_canonico text;
    v_publicado text;
begin
    select lower(checksum) into v_publicado from sigov.docker_manifest_entries where version = p_version;
    if v_publicado is null then
        raise exception 'MANIFEST_ENTRADA_AUSENTE: versao % processada sem entrada no staging do manifest', p_version;
    end if;
    -- Lista de valores historicos aceitos: remove o elemento vazio que o
    -- transporte espaco-separado produz quando knownChecksums e '{}'.
    select coalesce(array_agg(lower(x)), '{}') into p_permitidos
      from unnest(coalesce(p_permitidos, '{}')) as x(x)
     where x <> '';
    select lower(checksum) into v_ck_sidecar from sigov.docker_schema_migrations where version = p_version;
    select lower(checksum) into v_ck_canonico from sigov.schema_migrations where version = p_version;
    -- Regra 8: checksum em ledger so e aceito se igual ao publicado OU listado
    -- como knownChecksum (republicacao historica); nunca e sobrescrito.
    if coalesce(v_ck_sidecar, '') <> '' and not (v_ck_sidecar = v_publicado or v_ck_sidecar = any (p_permitidos)) then
        raise exception 'DIVERGENCIA_LEDGER_SIDECAR: versao % tem checksum % no sidecar, que nao e o publicado nem um knownChecksum do manifest; reconcilie antes de prosseguir',
            p_version, v_ck_sidecar;
    end if;
    if coalesce(v_ck_canonico, '') <> '' and not (v_ck_canonico = v_publicado or v_ck_canonico = any (p_permitidos)) then
        raise exception 'DIVERGENCIA_LEDGER_CANONICO: versao % tem checksum % no ledger do app, que nao e o publicado nem um knownChecksum do manifest; reconcilie antes de prosseguir',
            p_version, v_ck_canonico;
    end if;
end
$fn$;

create or replace function sigov.mig_provar_poscondicoes(p_version text)
returns void language plpgsql as $fn$
declare
    v_post text;
    v_ok boolean;
    v_probe text;
    r record;
begin
    select post_condition_sql into v_post from sigov.docker_manifest_entries where version = p_version;
    if v_post is not null then
        execute 'select (' || v_post || ')' into v_ok;
        if not coalesce(v_ok, false) then
            raise exception 'POSTCONDICAO_REPROVADA: %', p_version;
        end if;
    end if;
    for r in select * from sigov.docker_manifest_probes where version = p_version order by ordinal loop
        v_probe := null;
        execute 'select (' || r.sql || ')' into v_probe;
        if coalesce(v_probe, '') <> '' then
            raise exception 'POSTCONDICAO_PROBE_REPROVADA (% / %): %', p_version, r.name, v_probe;
        end if;
    end loop;
end
$fn$;

comment on table sigov.docker_manifest_entries is
    'RC-EVO-C 3.1: staging do parse estruturado (jsonb) do manifest.json; autoridade da fase 2 do sidecar; recriado a cada execucao.';
comment on table sigov.docker_manifest_probes is
    'RC-EVO-C 3.1: postConditionProbes nomeadas por versao, preservadas com fidelidade do manifest (contrato: NULL=sucesso, texto=diagnostico).';
comment on table sigov.docker_manifest_compat_before is
    'RC-EVO-C 3.1: scripts compatibilityBefore por versao (resolvidos em database/postgres/bootstrap); executados so quando a entrada e aplicada; sem ledger.';
comment on table sigov.docker_manifest_compat_after is
    'RC-EVO-C 3.1: scripts compatibilityAfterAll da raiz do manifest; executados apos a rodada quando houve aplicacao; sem ledger.';
SQL

  # Toda entrada com applyAutomatically=true precisa ter arquivo no disco;
  # entrada orfa de arquivo e erro nomeado (nao se aplica "o que existir" as cegas).
  missing_files=""
  while IFS= read -r entry; do
    [ -z "${entry}" ] && continue
    if [ ! -f "${migrations_dir}/${entry}" ]; then
      missing_files="${missing_files}${missing_files:+ }${entry}"
    fi
  done < <("${psql_base[@]}" -Atc "select file_name from sigov.docker_manifest_entries where apply_automatically order by version;")
  if [ -n "${missing_files}" ]; then
    echo "MANIFEST_ARQUIVO_AUSENTE: o manifest determina execucao automatica dos arquivos ausentes: ${missing_files}; instale os arquivos ou corrija o manifest."
    exit 1
  fi

  # Arquivos no disco fora do manifest: pulados explicitamente (o ManifestRunner
  # do app tambem os ignora); ficam visiveis no log de execucao.
  while IFS= read -r disk_file; do
    [ -z "${disk_file}" ] && continue
    # Interpolacao de variavel psql so ocorre no fluxo de script (stdin), nao em -c.
    in_manifest="$(printf "select exists(select 1 from sigov.docker_manifest_entries where file_name = :'df');\n" | "${psql_base[@]}" -At -v df="${disk_file}")"
    if [ "${in_manifest}" != "t" ]; then
      echo "Migration ${disk_file} ausente no manifest; pulando (fora da autoridade do manifest)."
    fi
  done < <(find "${migrations_dir}" -maxdepth 1 -type f -name '*.sql' -printf '%f\n' | sort)

  # Exclusoes (applyAutomatically=false): autoridade do manifest, reportadas a
  # cada execucao para transparencia (mesmo vocabulario do runner do app).
  while IFS= read -r excluded_version; do
    [ -z "${excluded_version}" ] && continue
    echo "Migration ${excluded_version} com applyAutomatically=false; pulando (autoridade do manifest)."
  done < <("${psql_base[@]}" -Atc "select version from sigov.docker_manifest_entries where not apply_automatically order by version;")

  # checksum_normalizado: mesmo calculo do ManifestRunner oficial (conteudo UTF-8
  # sem BOM e com CRLF normalizado para LF). O acervo ja e LF; a normalizacao
  # protege de checkout que converta finais de linha.
  checksum_normalizado() {
    sed -e '1s/^\xEF\xBB\xBF//' -e 's/\r$//' "$1" | sha256sum | awk '{print $1}'
  }

  # Autoridade de arquivos de compatibilidade: o ManifestRunner resolve os nomes
  # exclusivamente em database/postgres/bootstrap e recusa checksum divergente na
  # leitura do acervo; o sidecar valida os mesmos arquivos pelo mesmo calculo.
  bootstrap_dir="/database/postgres/bootstrap"
  while IFS=$'\t' read -r compat_scope compat_owner compat_file compat_checksum; do
    [ -z "${compat_file}" ] && continue
    case "${compat_file}" in
      ''|*[!0-9A-Za-z._-]*)
        echo "MANIFEST_INVALIDO: arquivo de compatibilidade com nome fora do vocabulario ([0-9A-Za-z._-]) em ${compat_scope} ${compat_owner}: '${compat_file}'"
        exit 1
        ;;
    esac
    if [ ! -f "${bootstrap_dir}/${compat_file}" ]; then
      echo "COMPATIBILIDADE_ARQUIVO_AUSENTE: ${compat_file} (referenciado por ${compat_scope} ${compat_owner}) nao existe em ${bootstrap_dir}; o ManifestRunner recusaria o acervo."
      exit 1
    fi
    if ! compat_actual="$(checksum_normalizado "${bootstrap_dir}/${compat_file}")"; then
      echo "FALHA_CHECKSUM_ARQUIVO: ${compat_file} (${compat_scope} ${compat_owner}); nao foi possivel ler o arquivo de compatibilidade."
      exit 1
    fi
    if [ "${compat_actual}" != "${compat_checksum}" ]; then
      echo "DIVERGENCIA_CHECKSUM_ARQUIVO_COMPAT: ${compat_file} (${compat_scope} ${compat_owner}) tem checksum ${compat_actual} e o manifest determina ${compat_checksum}; o arquivo alterado nao corresponde ao publicado."
      exit 1
    fi
  done < <("${psql_base[@]}" -At -F "$(printf '\t')" -c "select 'compatibilityBefore', version, file_name, checksum from sigov.docker_manifest_compat_before union all select 'compatibilityAfterAll', '-', file_name, checksum from sigov.docker_manifest_compat_after order by 1, 2, 3;")

  applied_now=0
  reconciled_now=0
  replicated_now=0
  skipped=0

  # Parametros por entrada trafegam pelo ambiente e entram no psql via \getenv:
  # nenhuma interpolacao de texto livre (descricao com aspas/acentos/multilinha)
  # toca o script gerado. A entrada e aplicada como UMA transacao: trava
  # advisory + conferencia de checksums + decisao do cliente (nao reaplica o que
  # consta em qualquer ledger) + DDL inline + prova de pos-condicoes + registro
  # nos dois ledgers + resultado nomeado no ultimo output.
  entry_template_head() {
    cat <<'SQL'
\set ON_ERROR_STOP on
begin;
SQL
    printf 'select pg_advisory_xact_lock(%s);\n' "${MIGRATION_LOCK}"
    cat <<'SQL'
\getenv v SIGOV_ENTRY_VERSION
\getenv d SIGOV_ENTRY_DESCRIPTION
\getenv c SIGOV_ENTRY_CATEGORY
\getenv f SIGOV_ENTRY_FILE_PATH
\getenv k SIGOV_ENTRY_CHECKSUM
\getenv p SIGOV_ENTRY_PERMITIDOS
select sigov.mig_conferir_checksums(:'v', string_to_array(:'p', ' '));
set local statement_timeout = '30min';
\set resultado PULADA
select (not exists(select 1 from sigov.docker_schema_migrations where version = :'v')
    and not exists(select 1 from sigov.schema_migrations where version = :'v')) as decidir_aplicar \gset
\if :decidir_aplicar
SQL
  }

  entry_template_tail() {
    cat <<'SQL'
select sigov.mig_provar_poscondicoes(:'v');
insert into sigov.docker_schema_migrations (version, file_path, checksum)
values (:'v', :'f', :'k')
on conflict (version) do nothing;
insert into sigov.schema_migrations (version, description, checksum, category, source, success, applied_at)
values (:'v', :'d', :'k', :'c', 'docker-sidecar', true, now())
on conflict (version) do nothing;
\set resultado APLICADA
\else
-- Consta em pelo menos um ledger: nunca reaplica. Reconcilia na direcao que
-- falta; para o ledger canonico, a prova de pos-condicoes e obrigatoria antes
-- de qualquer success novo (linha no ledger do sidecar por si so nao basta).
select (exists(select 1 from sigov.docker_schema_migrations where version = :'v')
    and not exists(select 1 from sigov.schema_migrations where version = :'v')) as decidir_reconciliar \gset
\if :decidir_reconciliar
select sigov.mig_provar_poscondicoes(:'v');
insert into sigov.schema_migrations (version, description, checksum, category, source, success, applied_at)
select e.version, e.description, lower(d.checksum), e.category, 'docker-sidecar', true, d.applied_at
from sigov.docker_manifest_entries e
join sigov.docker_schema_migrations d on d.version = e.version
where e.version = :'v'
on conflict (version) do nothing;
\set resultado RECONCILIADA
\endif
select (exists(select 1 from sigov.schema_migrations where version = :'v')
    and not exists(select 1 from sigov.docker_schema_migrations where version = :'v')) as decidir_replicar \gset
\if :decidir_replicar
insert into sigov.docker_schema_migrations (version, file_path, checksum, applied_at)
select m.version, m.description || '.sql', m.checksum, m.applied_at
from sigov.schema_migrations m
where m.version = :'v'
on conflict (version) do nothing;
\set resultado REPLICADA
\endif
\endif
commit;
select :'resultado' || ' ' || :'v';
SQL
  }

  while IFS=$'\t' read -r version file_name manifest_checksum permitted_list pre_list; do
    [ -z "${version}" ] && continue
    case "${version}" in
      ''|*[!0-9A-Za-z._-]*)
        echo "MANIFEST_INVALIDO: entrada com versao fora do vocabulario aceito ([0-9A-Za-z._-]): '${version}'"
        exit 1
        ;;
    esac
    if [ -z "${file_name}" ]; then
      echo "MANIFEST_INVALIDO: entrada ${version} sem arquivo no TSV do staging (parse corrompido?)."
      exit 1
    fi
    migration="${migrations_dir}/${file_name}"
    if ! checksum="$(checksum_normalizado "${migration}")"; then
      echo "FALHA_CHECKSUM_ARQUIVO: ${version} (${file_name}); nao foi possivel ler o arquivo para calcular o checksum."
      exit 1
    fi
    if [ "${checksum}" != "${manifest_checksum}" ]; then
      echo "DIVERGENCIA_CHECKSUM_ARQUIVO: ${version} (${file_name}) tem checksum ${checksum} e o manifest determina ${manifest_checksum}; o arquivo alterado nao corresponde ao publicado."
      exit 1
    fi

    entry_script="${tmp_dir}/entrada.sql"
    {
      entry_template_head
      # DDL inline (sem \i): o acervo nao usa meta-comandos psql nem transacoes
      # proprias, entao o conteudo entra cru dentro da transacao oficial.
      cat "${migration}"
      entry_template_tail
    } > "${entry_script}"

    export SIGOV_ENTRY_VERSION="${version}"
    export SIGOV_ENTRY_DESCRIPTION
    SIGOV_ENTRY_DESCRIPTION="$("${psql_base[@]}" -Atc "select coalesce(description, version) from sigov.docker_manifest_entries where version = '${version}';")"
    export SIGOV_ENTRY_CATEGORY
    SIGOV_ENTRY_CATEGORY="$("${psql_base[@]}" -Atc "select coalesce(category, 'functional') from sigov.docker_manifest_entries where version = '${version}';")"
    export SIGOV_ENTRY_FILE_PATH="${migration#/database/}"
    export SIGOV_ENTRY_CHECKSUM="${checksum}"
    # knownChecksums trafegam em base64 no TSV (transporte imune a separadores);
    # aqui viram a lista espacada que string_to_array(:'p', ' ') consome.
    permitted_list="$(printf '%s' "${permitted_list}" | base64 -d)"
    export SIGOV_ENTRY_PERMITIDOS="${permitted_list}"

    if ! "${psql_base[@]}" -q -At -o "${tmp_dir}/resultado.txt" -f "${entry_script}"; then
      echo "FALHA_APLICACAO_MIGRATION: ${version} (${file_name}); o erro nomeado acima impede continuar (nada foi confirmado nesta entrada)."
      exit 1
    fi
    outcome_line="$(tail -n 1 "${tmp_dir}/resultado.txt" || true)"
    outcome="${outcome_line%% *}"
    case "${outcome}" in
      APLICADA)
        echo "Migration ${version} aplicada; ledgers do sidecar e do app registrados na mesma transacao apos prova de pos-condicoes."
        applied_now=$((applied_now + 1))
        ;;
      RECONCILIADA)
        echo "Migration ${version} ja constava no ledger do sidecar; ledger canonico registrado apos prova de pos-condicoes (sem reaplicar DDL)."
        reconciled_now=$((reconciled_now + 1))
        ;;
      REPLICADA)
        echo "Migration ${version} ja constava no ledger do app; ledger do sidecar replicado (startup-first, sem reaplicar DDL)."
        replicated_now=$((replicated_now + 1))
        ;;
      PULADA)
        skipped=$((skipped + 1))
        ;;
      *)
        echo "FALHA_DECISAO_MIGRATION: resultado inesperado ao processar ${version}: '${outcome_line}'"
        exit 1
        ;;
    esac
  done < <("${psql_base[@]}" -At -F "$(printf '\t')" -c "select version, file_name, checksum, replace(encode(convert_to(array_to_string(known_checksums, ' '), 'UTF8'), 'base64'), E'\n', '') from sigov.docker_manifest_entries where apply_automatically order by version;")
  echo "Fase 2 concluida: ${applied_now} aplicacao(oes) nova(s), ${reconciled_now} reconciliacao(oes) para o ledger do app, ${replicated_now} replicacao(oes) para o ledger do sidecar, ${skipped} ja conciliadas (nenhuma releitura indevida de DDL)."
fi

# Guarda final de divergencia (regra 13): qualquer versao presente nos dois
# ledgers com checksums diferentes falha o fluxo, salvo quando AMBOS os lados
# estao dentro do conjunto aceito pelo manifest (checksum publicado ou listado
# como knownChecksum): espelhar historico legitimo nao e divergencia. Marker
# rows (version NULL) e checksums ausentes nao sao julgados.
"${psql_base[@]}" -q <<'SQL'
do $reconcile_final$
declare
    v_divergencias text;
begin
    select string_agg(format('%s (sidecar=%s, canonico=%s)', d.version, d.checksum, c.checksum), '; ' order by d.version)
      into v_divergencias
      from sigov.docker_schema_migrations d
      join sigov.schema_migrations c on c.version = d.version
      left join sigov.docker_manifest_entries e on e.version = d.version
     where d.version is not null
       and d.checksum is not null and c.checksum is not null
       and lower(d.checksum) <> lower(c.checksum)
       and (
           e.version is null
           or not (
               lower(d.checksum) = lower(e.checksum)
               or exists (select 1 from unnest(e.known_checksums) as k(x)
                          where lower(k.x) = lower(d.checksum))
           )
           or not (
               lower(c.checksum) = lower(e.checksum)
               or exists (select 1 from unnest(e.known_checksums) as k2(x)
                          where lower(k2.x) = lower(c.checksum))
           )
       );
    if v_divergencias is not null then
        raise exception 'DIVERGENCIA_LEDGER_LADO_A_LADO: %', v_divergencias;
    end if;
end
$reconcile_final$;
SQL

echo "Migrations aplicadas com sucesso; ledgers do sidecar e do app reconciliados."
