-- RC50.36: arrecadação e dívida ativa preparatória, sem integração externa automática.
create schema if not exists sigov;
create or replace function sigov.rc50_36_ensure_common_columns(p_table regclass)
returns void
language plpgsql
as $$
begin
    execute format('alter table %s add column if not exists is_deleted boolean', p_table);
    execute format('update %s set is_deleted=false where is_deleted is null', p_table);
    execute format('alter table %s alter column is_deleted set default false', p_table);
    execute format('alter table %s alter column is_deleted set not null', p_table);
    execute format('alter table %s add column if not exists ativo boolean', p_table);
    execute format('update %s set ativo=true where ativo is null', p_table);
    execute format('alter table %s alter column ativo set default true', p_table);
    execute format('alter table %s add column if not exists dados jsonb', p_table);
    execute format('update %s set dados=''{}''::jsonb where dados is null', p_table);
    execute format('alter table %s alter column dados set default ''{}''::jsonb', p_table);
    execute format('alter table %s add column if not exists auditoria jsonb', p_table);
    execute format('update %s set auditoria=''{}''::jsonb where auditoria is null', p_table);
    execute format('alter table %s alter column auditoria set default ''{}''::jsonb', p_table);
    execute format('alter table %s add column if not exists correlation_id varchar(100)', p_table);
    execute format('alter table %s add column if not exists created_at timestamptz', p_table);
    execute format('update %s set created_at=now() where created_at is null', p_table);
    execute format('alter table %s alter column created_at set default now()', p_table);
    execute format('alter table %s add column if not exists updated_at timestamptz', p_table);
    execute format('alter table %s add column if not exists deleted_at timestamptz', p_table);
    execute format('alter table %s add column if not exists created_by bigint', p_table);
    execute format('alter table %s add column if not exists updated_by bigint', p_table);
    execute format('alter table %s add column if not exists deleted_by bigint', p_table);
end $$;
create table if not exists sigov.tributario_contribuinte (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, nome varchar(180) not null,
 documento varchar(20), inscricao varchar(60), tipo varchar(20) not null default 'PESSOA', status varchar(24) not null default 'ABERTO',
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint
);

do $$ declare t text; begin foreach t in array array['tributario_cadastro_imobiliario','tributario_cadastro_economico','tributario_tributo'] loop execute format('create table if not exists sigov.%I (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, contribuinte_id bigint references sigov.tributario_contribuinte(id), inscricao varchar(60), codigo varchar(60), descricao text, status varchar(24) not null default ''ABERTO'', dados jsonb not null default ''{}''::jsonb, auditoria jsonb not null default ''{}''::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint)',t); end loop; end $$;

create table if not exists sigov.tributario_lancamento (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, exercicio_id bigint, contribuinte_id bigint not null references sigov.tributario_contribuinte(id), tributo_id bigint not null references sigov.tributario_tributo(id),
 codigo varchar(60) not null, descricao text, competencia date not null, data_vencimento date not null, valor_original numeric(18,2) not null check(valor_original>0), valor_multa numeric(18,2) not null default 0,
 valor_juros numeric(18,2) not null default 0, valor_correcao numeric(18,2) not null default 0, valor_total numeric(18,2) not null check(valor_total>0), saldo numeric(18,2) not null check(saldo>=0),
 status varchar(24) not null default 'RASCUNHO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint,
 unique(tenant_id,entidade_id,codigo)
);
create table if not exists sigov.tributario_lancamento_item (id bigserial primary key, tenant_id bigint not null, lancamento_id bigint not null references sigov.tributario_lancamento(id), descricao text not null, valor numeric(18,2) not null check(valor>0), dados jsonb not null default '{}'::jsonb, is_deleted boolean not null default false, created_at timestamptz not null default now());
create table if not exists sigov.tributario_guia (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, lancamento_id bigint not null references sigov.tributario_lancamento(id), contribuinte_id bigint not null references sigov.tributario_contribuinte(id), codigo varchar(80) not null, data_vencimento date not null, valor_total numeric(18,2) not null check(valor_total>0), saldo numeric(18,2) not null check(saldo>=0), status varchar(24) not null default 'EM_ABERTO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint, unique(tenant_id,codigo));
create table if not exists sigov.tributario_pagamento (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, guia_id bigint not null references sigov.tributario_guia(id), valor numeric(18,2) not null check(valor>0), data_pagamento date not null, status varchar(24) not null default 'PAGO', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint);
create table if not exists sigov.tributario_divida_ativa (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, contribuinte_id bigint not null references sigov.tributario_contribuinte(id), lancamento_id bigint not null references sigov.tributario_lancamento(id), inscricao varchar(80) not null, data_inscricao date not null, data_prescricao_alerta date, valor_total numeric(18,2) not null, saldo numeric(18,2) not null, status varchar(24) not null default 'INSCRITO_DIVIDA', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint, unique(tenant_id,lancamento_id), unique(tenant_id,inscricao));
do $$ declare t text; begin foreach t in array array['tributario_parcelamento','tributario_parcelamento_parcela','tributario_divida_movimento','tributario_cobranca','tributario_baixa','tributario_suspensao','tributario_evento'] loop execute format('create table if not exists sigov.%I (id bigserial primary key, tenant_id bigint not null, entidade_id bigint not null, contribuinte_id bigint, divida_ativa_id bigint, codigo varchar(80), descricao text, competencia date, data_vencimento date, valor_original numeric(18,2) not null default 0, valor_total numeric(18,2) not null default 0, saldo numeric(18,2) not null default 0, status varchar(24) not null default ''EM_ABERTO'', dados jsonb not null default ''{}''::jsonb, auditoria jsonb not null default ''{}''::jsonb, correlation_id varchar(100), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint)',t); end loop; end $$;

do $$
declare
    tabela regclass;
begin
    foreach tabela in array array[
        'sigov.tributario_contribuinte'::regclass,
        'sigov.tributario_cadastro_imobiliario'::regclass,
        'sigov.tributario_cadastro_economico'::regclass,
        'sigov.tributario_tributo'::regclass,
        'sigov.tributario_lancamento'::regclass,
        'sigov.tributario_lancamento_item'::regclass,
        'sigov.tributario_guia'::regclass,
        'sigov.tributario_pagamento'::regclass,
        'sigov.tributario_divida_ativa'::regclass,
        'sigov.tributario_parcelamento'::regclass,
        'sigov.tributario_parcelamento_parcela'::regclass,
        'sigov.tributario_divida_movimento'::regclass,
        'sigov.tributario_cobranca'::regclass,
        'sigov.tributario_baixa'::regclass,
        'sigov.tributario_suspensao'::regclass,
        'sigov.tributario_evento'::regclass
    ]
    loop
        perform sigov.rc50_36_ensure_common_columns(tabela);
    end loop;
end $$;

alter table sigov.tributario_contribuinte add column if not exists documento varchar(20);
alter table sigov.tributario_contribuinte add column if not exists tenant_id bigint;

do $$
begin
    if exists (
        select 1
          from information_schema.columns
         where table_schema='sigov'
           and table_name='tributario_contribuinte'
           and column_name='tenant_id'
           and is_nullable='YES'
    ) and exists (
        select 1 from sigov.tributario_contribuinte where tenant_id is null
    ) then
        raise exception 'RC50.36: sigov.tributario_contribuinte possui registros sem tenant_id. Corrija o tenant antes de criar índices multi-tenant.';
    end if;
end $$;

create unique index if not exists ux_tri_contribuinte_documento on sigov.tributario_contribuinte(tenant_id,documento) where documento is not null and is_deleted=false;
create index if not exists ix_tri_lancamento_tenant_status on sigov.tributario_lancamento(tenant_id,status,data_vencimento) where is_deleted=false;
create index if not exists ix_tri_guia_contribuinte on sigov.tributario_guia(tenant_id,contribuinte_id,status) where is_deleted=false;
create index if not exists ix_tri_divida_prescricao on sigov.tributario_divida_ativa(tenant_id,data_prescricao_alerta,status) where is_deleted=false;

drop function if exists sigov.rc50_36_ensure_common_columns(regclass);
