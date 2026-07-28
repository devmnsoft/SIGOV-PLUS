-- Pós-RC 27B.2: reconciliação aditiva e schema-safe do contrato operacional de Tarefas.
create schema if not exists sigov;

-- A migration pode ser reaplicada sobre qualquer forma histórica da tabela. Quando
-- executada isoladamente em um banco sem o núcleo operacional, ela é um no-op seguro.
do $migration$
begin
  if to_regclass('sigov.tarefa') is null then
    return;
  end if;

  alter table sigov.tarefa add column if not exists descricao text null;
  alter table sigov.tarefa add column if not exists status text;
  alter table sigov.tarefa add column if not exists prioridade text;
  alter table sigov.tarefa add column if not exists responsavel_id bigint null;
  alter table sigov.tarefa add column if not exists is_deleted boolean not null default false;
  alter table sigov.tarefa add column if not exists updated_at timestamptz not null default now();
  alter table sigov.tarefa add column if not exists updated_by bigint null;
  alter table sigov.tarefa add column if not exists correlation_id uuid null;
  alter table sigov.tarefa add column if not exists version bigint not null default 1;
  alter table sigov.tarefa add column if not exists prazo_em timestamptz null;
  alter table sigov.tarefa add column if not exists entidade text null;
  alter table sigov.tarefa add column if not exists entidade_id text null;
  alter table sigov.tarefa add column if not exists concluida_em timestamptz null;
  alter table sigov.tarefa add column if not exists cancelada_em timestamptz null;
  alter table sigov.tarefa add column if not exists motivo_cancelamento text null;

  -- O núcleo 27B.1 usava text. Valores UUID são preservados e valores históricos
  -- inválidos tornam-se nulos, evitando casts Dapper diferentes por consumidor.
  if exists (
    select 1 from information_schema.columns
    where table_schema = 'sigov' and table_name = 'tarefa'
      and column_name = 'correlation_id' and data_type <> 'uuid'
  ) then
    alter table sigov.tarefa alter column correlation_id type uuid using
      case when correlation_id::text ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'
        then correlation_id::text::uuid else null end;
  end if;

  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='prazo_at') then
    execute 'update sigov.tarefa set prazo_em = coalesce(prazo_em, prazo_at) where prazo_em is null';
    comment on column sigov.tarefa.prazo_at is 'LEGADO: use prazo_em; mantida para compatibilidade nesta versão.';
  end if;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa' and column_name='entidade_tipo') then
    execute 'update sigov.tarefa set entidade = coalesce(entidade, entidade_tipo) where entidade is null';
    comment on column sigov.tarefa.entidade_tipo is 'LEGADO: use entidade; mantida para compatibilidade nesta versão.';
  end if;

  update sigov.tarefa set status = 'ABERTA' where status is null or upper(btrim(status)) = 'PENDENTE';
  update sigov.tarefa set prioridade = 'NORMAL' where prioridade is null or btrim(prioridade) = '';
  alter table sigov.tarefa alter column status set default 'ABERTA';
  alter table sigov.tarefa alter column status set not null;
  alter table sigov.tarefa alter column prioridade set default 'NORMAL';
  alter table sigov.tarefa alter column prioridade set not null;
end
$migration$;

-- Helper local: só cria o índice quando a tabela e todas as colunas existem e
-- rejeita definição sem expressão ou predicate, evitando SQL parcial/acidental.
create or replace function pg_temp.ensure_schema_safe_index(
  qualified_table text, index_name text, required_columns text[], expression_sql text, predicate_sql text
) returns void language plpgsql as $helper$
declare missing_column text;
begin
  if to_regclass(qualified_table) is null or nullif(btrim(expression_sql), '') is null or nullif(btrim(predicate_sql), '') is null then
    return;
  end if;
  select column_name into missing_column
  from unnest(required_columns) column_name
  where not exists (
    select 1 from information_schema.columns c
    where quote_ident(c.table_schema) || '.' || quote_ident(c.table_name) = qualified_table
      and c.column_name = column_name
  ) limit 1;
  if missing_column is null then
    execute format('create index if not exists %I on %s (%s) where %s', index_name, qualified_table, expression_sql, predicate_sql);
  end if;
end
$helper$;

select pg_temp.ensure_schema_safe_index('sigov.tarefa', 'ix_tarefa_tenant_status', array['tenant_id','status','is_deleted'], 'tenant_id, status', 'is_deleted = false');
select pg_temp.ensure_schema_safe_index('sigov.tarefa', 'ix_tarefa_tenant_responsavel_prazo', array['tenant_id','responsavel_id','prazo_em','is_deleted'], 'tenant_id, responsavel_id, prazo_em', 'is_deleted = false');
select pg_temp.ensure_schema_safe_index('sigov.tarefa', 'ix_tarefa_tenant_version', array['tenant_id','version','is_deleted'], 'tenant_id, version', 'is_deleted = false');
select pg_temp.ensure_schema_safe_index('sigov.tarefa', 'ix_tarefa_tenant_entidade', array['tenant_id','entidade','entidade_id','is_deleted'], 'tenant_id, entidade, entidade_id', 'is_deleted = false');

do $migration$
begin
  if to_regclass('sigov.tarefa_vinculo') is null then return; end if;
  alter table sigov.tarefa_vinculo add column if not exists entidade text null;
  alter table sigov.tarefa_vinculo add column if not exists entidade_id text null;
  if exists (select 1 from information_schema.columns where table_schema='sigov' and table_name='tarefa_vinculo' and column_name='tipo') then
    execute 'update sigov.tarefa_vinculo set entidade = coalesce(entidade, tipo) where entidade is null';
    comment on column sigov.tarefa_vinculo.tipo is 'LEGADO: use entidade; mantida para compatibilidade nesta versão.';
  end if;
end
$migration$;
