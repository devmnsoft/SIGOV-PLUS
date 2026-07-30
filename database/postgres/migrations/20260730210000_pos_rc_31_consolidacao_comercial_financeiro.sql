-- Pós-RC 31: estruturas incrementais de concorrência e integração. Não remove nem recria dados.
create table if not exists sigov.enterprise_numeracao_comercial (
 tenant_id uuid not null, tipo varchar(40) not null, ano integer not null,
 ultimo_numero bigint not null default 0 check (ultimo_numero >= 0),
 version bigint not null default 1, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 correlation_id varchar(100), primary key (tenant_id,tipo,ano)
);
create table if not exists sigov.enterprise_comercial_idempotencia (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, operacao varchar(80) not null,
 chave varchar(200) not null, recurso_id uuid not null, version bigint not null default 1,
 created_at timestamptz not null default now(), created_by varchar(100) not null, correlation_id varchar(100) not null,
 is_deleted boolean not null default false, deleted_at timestamptz,
 constraint ck_comercial_idempotencia_chave check (length(trim(chave)) between 1 and 200)
);
create unique index if not exists ux_comercial_idempotencia_ativa on sigov.enterprise_comercial_idempotencia(tenant_id,operacao,chave) where not is_deleted;
create unique index if not exists ux_estoque_reserva_pedido_item_ativa on sigov.enterprise_estoque_reserva(tenant_id,pedido_item_id) where not is_deleted;
create table if not exists sigov.enterprise_financeiro_inbox (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, event_id uuid not null, event_type varchar(160) not null,
 event_version integer not null check(event_version > 0), payload jsonb not null, status varchar(30) not null default 'PENDENTE',
 erro varchar(1000), tentativas integer not null default 0 check(tentativas >= 0), processado_em timestamptz,
 version bigint not null default 1, correlation_id varchar(100) not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 is_deleted boolean not null default false, deleted_at timestamptz
);
create unique index if not exists ux_financeiro_inbox_evento on sigov.enterprise_financeiro_inbox(tenant_id,event_id) where not is_deleted;
create table if not exists sigov.enterprise_financeiro_integracao_pedido (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, pedido_id uuid not null, cliente_id uuid not null,
 conta_receber_id uuid, status varchar(30) not null, pendencia varchar(500),
 version bigint not null default 1, correlation_id varchar(100) not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 is_deleted boolean not null default false, deleted_at timestamptz
);
create unique index if not exists ux_financeiro_integracao_pedido on sigov.enterprise_financeiro_integracao_pedido(tenant_id,pedido_id) where not is_deleted;
create table if not exists sigov.enterprise_financeiro_regra_aprovacao (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, nome varchar(150) not null,
 valor_minimo numeric(18,2) not null default 0 check(valor_minimo >= 0), valor_maximo numeric(18,2),
 nivel smallint not null default 1 check(nivel > 0), segregacao_funcao boolean not null default true, ativo boolean not null default true,
 version bigint not null default 1, correlation_id varchar(100), created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 is_deleted boolean not null default false, deleted_at timestamptz,
 check(valor_maximo is null or valor_maximo >= valor_minimo)
);
create index if not exists ix_financeiro_regra_aprovacao_tenant_valor on sigov.enterprise_financeiro_regra_aprovacao(tenant_id,valor_minimo,valor_maximo) where ativo and not is_deleted;
