create schema if not exists sigov;
create extension if not exists pgcrypto;

create table if not exists sigov.enterprise_anexo (
    id uuid primary key default gen_random_uuid(),
    tenant_id uuid not null,
    entidade text not null,
    entidade_id uuid not null,
    documento_id bigint not null,
    status text not null default 'ATIVO',
    classificacao_lgpd text not null default 'INTERNO',
    created_at timestamptz not null default now(),
    created_by text null,
    is_deleted boolean not null default false,
    correlation_id text null,
    constraint ck_enterprise_anexo_status check (status in ('ATIVO','REMOVIDO','BLOQUEADO')),
    constraint ck_enterprise_anexo_lgpd check (classificacao_lgpd in ('PUBLICO','INTERNO','RESTRITO','SENSIVEL'))
);

create index if not exists ix_enterprise_anexo_tenant_entidade on sigov.enterprise_anexo (tenant_id, entidade, entidade_id) where is_deleted = false;
create index if not exists ix_enterprise_anexo_documento on sigov.enterprise_anexo (tenant_id, documento_id) where is_deleted = false;
create index if not exists ix_enterprise_anexo_created_at on sigov.enterprise_anexo (created_at desc);

insert into sigov.enterprise_anexo (tenant_id, entidade, entidade_id, documento_id, status, classificacao_lgpd, created_by, correlation_id)
select 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid, 'cliente', '22222222-2222-2222-2222-222222222222'::uuid, 1, 'ATIVO', 'INTERNO', 'seed.pos_rc_11', 'pos-rc-11-seed'
where not exists (
  select 1 from sigov.enterprise_anexo where tenant_id='aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa'::uuid and entidade='cliente' and entidade_id='22222222-2222-2222-2222-222222222222'::uuid and documento_id=1
);
