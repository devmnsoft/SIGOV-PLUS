begin;
create schema if not exists sigov;
create extension if not exists pgcrypto;

create table if not exists sigov.tarefa (
  id bigserial primary key, tenant_id bigint not null, titulo text not null, descricao text null, status text not null default 'ABERTA', prioridade text not null default 'NORMAL', responsavel_id bigint null, prazo_em timestamptz null, origem text null, entidade text null, entidade_id text null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null
);
create table if not exists sigov.tarefa_historico (id bigserial primary key, tenant_id bigint not null, tarefa_id bigint not null references sigov.tarefa(id), acao text not null, antes_json jsonb null, depois_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, ip_address text null, user_agent text null, correlation_id text null);
create table if not exists sigov.tarefa_comentario (id bigserial primary key, tenant_id bigint not null, tarefa_id bigint not null references sigov.tarefa(id), comentario text not null, classificacao_acesso text not null default 'INTERNO', created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.tarefa_anexo (id bigserial primary key, tenant_id bigint not null, tarefa_id bigint not null references sigov.tarefa(id), storage_key text not null, nome_arquivo text not null, content_type text null, classificacao_acesso text not null default 'INTERNO', created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.tarefa_vinculo (id bigserial primary key, tenant_id bigint not null, tarefa_id bigint not null references sigov.tarefa(id), entidade text not null, entidade_id text not null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);

create table if not exists sigov.agenda_compromisso (id bigserial primary key, tenant_id bigint not null, titulo text not null, descricao text null, inicio_em timestamptz not null, fim_em timestamptz not null, status text not null default 'AGENDADO', recorrencia text null, origem text null, entidade text null, entidade_id text null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.agenda_participante (id bigserial primary key, tenant_id bigint not null, compromisso_id bigint not null references sigov.agenda_compromisso(id), usuario_id bigint not null, status text not null default 'PENDENTE', created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.agenda_lembrete (id bigserial primary key, tenant_id bigint not null, compromisso_id bigint not null references sigov.agenda_compromisso(id), lembrar_em timestamptz not null, status text not null default 'PENDENTE', created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.prazo_operacional (id bigserial primary key, tenant_id bigint not null, titulo text not null, tipo text not null default 'INTERNO', vence_em timestamptz not null, status text not null default 'ABERTO', tarefa_id bigint null references sigov.tarefa(id), origem text null, entidade text null, entidade_id text null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.prazo_historico (id bigserial primary key, tenant_id bigint not null, prazo_id bigint not null references sigov.prazo_operacional(id), acao text not null, antes_json jsonb null, depois_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, correlation_id text null);

create table if not exists sigov.notificacao (id bigserial primary key, tenant_id bigint not null, tipo text not null, titulo text not null, mensagem text null, modulo text null, prioridade text not null default 'NORMAL', origem text null, entidade text null, entidade_id text null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.notificacao_usuario (id bigserial primary key, tenant_id bigint not null, notificacao_id bigint null references sigov.notificacao(id), usuario_id bigint not null, tipo text not null default 'operacional', titulo text not null, lida boolean not null default false, lida_em timestamptz null, arquivada boolean not null default false, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.notificacao_preferencia (id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, tipo text not null, habilitada boolean not null default true, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null, unique (tenant_id, usuario_id, tipo));

create table if not exists sigov.kanban_quadro (id bigserial primary key, tenant_id bigint not null, nome text not null, origem text not null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.kanban_coluna (id bigserial primary key, tenant_id bigint not null, quadro_id bigint not null references sigov.kanban_quadro(id), nome text not null, status text not null, ordem int not null default 0, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.kanban_card (id bigserial primary key, tenant_id bigint not null, quadro_id bigint null references sigov.kanban_quadro(id), origem text not null, entidade_id bigint not null, titulo text not null, coluna text not null, ordem int not null default 0, responsavel_id bigint null, prioridade text null, prazo_em timestamptz null, sla text null, version int not null default 1, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz not null default now(), updated_by bigint null, is_deleted boolean not null default false, deleted_at timestamptz null, deleted_by bigint null, correlation_id text null);
create table if not exists sigov.kanban_historico (id bigserial primary key, tenant_id bigint not null, card_id bigint not null references sigov.kanban_card(id), acao text not null, antes_json jsonb null, depois_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, correlation_id text null);

create table if not exists sigov.outbox_evento (id bigserial primary key, tenant_id bigint not null, event_id uuid not null, event_type text not null, event_version int not null default 1, aggregate_type text not null, aggregate_id text not null, user_id bigint null, correlation_id text null, occurred_at timestamptz not null default now(), payload jsonb not null default '{}'::jsonb, status text not null default 'PENDING', attempts int not null default 0, next_attempt_at timestamptz null, idempotency_key text null unique);
alter table sigov.outbox_evento add column if not exists event_id uuid;
alter table sigov.outbox_evento add column if not exists event_type text;
alter table sigov.outbox_evento add column if not exists event_version int not null default 1;
alter table sigov.outbox_evento add column if not exists aggregate_type text;
alter table sigov.outbox_evento add column if not exists aggregate_id text;
alter table sigov.outbox_evento add column if not exists user_id bigint;
alter table sigov.outbox_evento add column if not exists occurred_at timestamptz not null default now();
alter table sigov.outbox_evento add column if not exists attempts int not null default 0;
alter table sigov.outbox_evento add column if not exists next_attempt_at timestamptz;
alter table sigov.outbox_evento add column if not exists idempotency_key text;
create unique index if not exists ux_outbox_evento_idempotency_key on sigov.outbox_evento (idempotency_key) where idempotency_key is not null;
do $migration$
declare
  columns text[];
  event_type_source text := quote_literal('operacional.legado');
  aggregate_type_source text := quote_literal('legado');
  aggregate_id_source text := 'id::text';
  attempts_source text := '0';
  next_attempt_source text := 'now()';
begin
  select array_agg(column_name) into columns
    from information_schema.columns
   where table_schema = 'sigov' and table_name = 'outbox_evento';

  if 'tipo_evento' = any(columns) then event_type_source := 'tipo_evento::text';
  elsif 'evento' = any(columns) then event_type_source := 'evento::text'; end if;

  if 'entidade_tipo' = any(columns) then aggregate_type_source := 'entidade_tipo::text';
  elsif 'agregado' = any(columns) then aggregate_type_source := 'agregado::text'; end if;

  if 'entidade_id' = any(columns) then aggregate_id_source := 'entidade_id::text';
  elsif 'agregado_id' = any(columns) then aggregate_id_source := 'agregado_id::text'; end if;

  if 'tentativas' = any(columns) then attempts_source := 'tentativas'; end if;
  if 'proxima_tentativa_at' = any(columns) then next_attempt_source := 'proxima_tentativa_at';
  elsif 'created_at' = any(columns) then next_attempt_source := 'created_at'; end if;

  execute format(
    'update sigov.outbox_evento set event_id=coalesce(event_id,gen_random_uuid()), event_type=coalesce(event_type,%s,%L), aggregate_type=coalesce(aggregate_type,%s,%L), aggregate_id=coalesce(aggregate_id,%s), attempts=coalesce(attempts,%s,0), next_attempt_at=coalesce(next_attempt_at,%s,now()) where event_id is null or event_type is null or aggregate_type is null or aggregate_id is null or attempts is null',
    event_type_source, 'operacional.legado', aggregate_type_source, 'legado', aggregate_id_source, attempts_source, next_attempt_source);
end
$migration$;
alter table sigov.outbox_evento alter column event_id set not null;
alter table sigov.outbox_evento alter column event_type set not null;
alter table sigov.outbox_evento alter column aggregate_type set not null;
alter table sigov.outbox_evento alter column aggregate_id set not null;

-- This migration can run over schemas created by releases that predate the
-- canonical operational contract.  CREATE TABLE IF NOT EXISTS deliberately
-- does not mutate those tables, so indexes must not assume canonical columns.
create or replace function pg_temp.create_index_when_columns_exist(
  p_schema_name text,
  p_table_name text,
  p_index_name text,
  p_required_columns text[],
  p_index_expression text,
  p_predicate text default null
) returns void
language plpgsql
as $helper$
declare
  missing_columns text[];
  statement text;
begin
  if to_regclass(format('%I.%I', p_schema_name, p_table_name)) is null then
    raise notice 'Skipping index %: table %.% does not exist',
      p_index_name, p_schema_name, p_table_name;
    return;
  end if;

  select array_agg(required_column order by required_column)
    into missing_columns
    from unnest(p_required_columns) required_column
   where not exists (
     select 1
       from information_schema.columns
      where table_schema = p_schema_name
        and table_name = p_table_name
        and column_name = required_column
   );

  if cardinality(missing_columns) > 0 then
    raise notice 'Skipping index % on %.%: missing columns %',
      p_index_name, p_schema_name, p_table_name, missing_columns;
    return;
  end if;

  statement := format(
    'create index if not exists %I on %I.%I (%s)',
    p_index_name, p_schema_name, p_table_name, p_index_expression);
  if nullif(btrim(p_predicate), '') is not null then
    statement := statement || ' where ' || p_predicate;
  end if;

  -- Do not catch execution errors: malformed expressions and predicates must
  -- fail the migration instead of being mistaken for historical compatibility.
  execute statement;
end
$helper$;

select pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_tenant_status', array['tenant_id', 'status'], 'tenant_id, status');
select pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_responsavel_prazo', array['tenant_id', 'responsavel_id', 'prazo_em'], 'tenant_id, responsavel_id, prazo_em');
select pg_temp.create_index_when_columns_exist('sigov', 'agenda_compromisso', 'ix_agenda_periodo', array['tenant_id', 'inicio_em', 'fim_em'], 'tenant_id, inicio_em, fim_em');
select pg_temp.create_index_when_columns_exist('sigov', 'prazo_operacional', 'ix_prazo_vencimento', array['tenant_id', 'status', 'vence_em'], 'tenant_id, status, vence_em');
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao_usuario', 'ix_notificacao_usuario_lida', array['tenant_id', 'usuario_id', 'lida'], 'tenant_id, usuario_id, lida');
select pg_temp.create_index_when_columns_exist('sigov', 'kanban_card', 'ix_kanban_filtros', array['tenant_id', 'origem', 'responsavel_id', 'sla', 'coluna'], 'tenant_id, origem, responsavel_id, sla, coluna');
select pg_temp.create_index_when_columns_exist('sigov', 'outbox_evento', 'ix_outbox_evento_status', array['status', 'next_attempt_at'], 'status, next_attempt_at');
commit;
