-- SIGOV+ RC38E
-- A migration RC32 adiciona colunas condicionalmente, mas cria o índice de forma
-- incondicional. A tabela é parte da ponte entre o Enterprise e o Financeiro Core;
-- quando uma instalação anterior não a criou, o contrato mínimo precisa existir.

create schema if not exists sigov;
create extension if not exists pgcrypto;

create table if not exists sigov.enterprise_integracao_financeira (
    id uuid primary key default gen_random_uuid(),
    tenant_id uuid,
    origem_tipo varchar(80),
    origem_id uuid,
    conta_receber_core_id bigint,
    tenant_core_id bigint,
    status varchar(40) not null default 'PENDENTE',
    payload_json jsonb not null default '{}'::jsonb,
    erro text,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

alter table sigov.enterprise_integracao_financeira
    add column if not exists id uuid default gen_random_uuid(),
    add column if not exists tenant_id uuid,
    add column if not exists origem_tipo varchar(80),
    add column if not exists origem_id uuid,
    add column if not exists conta_receber_core_id bigint,
    add column if not exists tenant_core_id bigint,
    add column if not exists status varchar(40) default 'PENDENTE',
    add column if not exists payload_json jsonb default '{}'::jsonb,
    add column if not exists erro text,
    add column if not exists created_at timestamptz default now(),
    add column if not exists updated_at timestamptz default now();

-- Falhar cedo caso uma instalação legada possua colunas homônimas com tipos
-- incompatíveis. Alterar tipos automaticamente poderia corromper a ponte UUID -> bigint.
do $$
declare
    invalid_column text;
begin
    select expected.column_name
      into invalid_column
      from (values
        ('id', 'uuid'),
        ('tenant_id', 'uuid'),
        ('origem_tipo', 'character varying'),
        ('origem_id', 'uuid'),
        ('conta_receber_core_id', 'bigint'),
        ('tenant_core_id', 'bigint'),
        ('status', 'character varying'),
        ('payload_json', 'jsonb'),
        ('erro', 'text'),
        ('created_at', 'timestamp with time zone'),
        ('updated_at', 'timestamp with time zone')
      ) expected(column_name, data_type)
      join information_schema.columns actual
        on actual.table_schema = 'sigov'
       and actual.table_name = 'enterprise_integracao_financeira'
       and actual.column_name = expected.column_name
     where actual.data_type <> expected.data_type
     limit 1;

    if invalid_column is not null then
        raise exception 'Contrato legado incompatível em sigov.enterprise_integracao_financeira.%', invalid_column;
    end if;
end
$$;
