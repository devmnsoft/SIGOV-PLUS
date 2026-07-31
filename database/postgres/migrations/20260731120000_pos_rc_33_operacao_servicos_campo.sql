-- Pós-RC 33: operação de campo, evidências, aceite e custos.
create extension if not exists pgcrypto;

create table if not exists sigov.os_equipe (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, nome varchar(160) not null,
 coordenador_usuario_id uuid, regiao varchar(160), especialidades text[] not null default '{}', disponivel boolean not null default true,
 version bigint not null default 1, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 created_by varchar(80) not null, updated_by varchar(80) not null, correlation_id varchar(120), unique(tenant_id,nome)
);
create table if not exists sigov.os_tecnico (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, usuario_id uuid not null, equipe_id uuid references sigov.os_equipe(id), nome varchar(200) not null,
 especialidade varchar(160), habilidades text[] not null default '{}', certificacoes text[] not null default '{}', regiao varchar(160), status varchar(30) not null default 'DISPONIVEL',
 inicio_jornada time, fim_jornada time, capacidade_diaria int not null default 8 check(capacidade_diaria>0), custo_hora numeric(18,4) not null default 0 check(custo_hora>=0),
 version bigint not null default 1, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 created_by varchar(80) not null, updated_by varchar(80) not null, correlation_id varchar(120), unique(tenant_id,usuario_id)
);
create index if not exists ix_os_tecnico_filtro on sigov.os_tecnico(tenant_id,equipe_id,regiao,status) where not is_deleted;

create table if not exists sigov.os_evidencia (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, ordem_servico_id uuid not null references sigov.os_ordem_servico(id), documento_ged_id uuid not null,
 tipo varchar(30) not null check(tipo in ('FOTO_ANTES','FOTO_DEPOIS','RELATORIO','LAUDO','COMPROVANTE','DOCUMENTO')), nome varchar(250) not null, idempotency_key varchar(200) not null,
 version bigint not null default 1, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 created_by varchar(80) not null, updated_by varchar(80) not null, correlation_id varchar(120), unique(tenant_id,idempotency_key)
);
create index if not exists ix_os_evidencia_ordem on sigov.os_evidencia(tenant_id,ordem_servico_id,created_at) where not is_deleted;
create table if not exists sigov.os_aceite (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, ordem_servico_id uuid not null references sigov.os_ordem_servico(id), nome varchar(200) not null,
 documento_mascarado varchar(40) not null, confirmado boolean not null check(confirmado), observacao text, aceite_em timestamptz not null default now(), evidencia_assinatura_id uuid,
 hash_evidencia varchar(64) not null, idempotency_key varchar(200) not null, version bigint not null default 1, is_deleted boolean not null default false,
 created_at timestamptz not null default now(), updated_at timestamptz not null default now(), created_by varchar(80) not null, updated_by varchar(80) not null,
 correlation_id varchar(120), unique(tenant_id,idempotency_key)
);
create index if not exists ix_os_aceite_ordem on sigov.os_aceite(tenant_id,ordem_servico_id,aceite_em desc) where not is_deleted;
alter table sigov.os_ordem_servico add column if not exists adicionais numeric(18,2) not null default 0,
 add column if not exists descontos numeric(18,2) not null default 0,
 add column if not exists valor_cobrado numeric(18,2) not null default 0;
