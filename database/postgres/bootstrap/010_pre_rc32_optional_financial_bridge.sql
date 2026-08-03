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
    add column if not exists conta_receber_core_id bigint,
    add column if not exists tenant_core_id bigint;
