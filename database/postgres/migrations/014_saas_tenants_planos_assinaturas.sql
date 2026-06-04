create table if not exists sigov.tenant (
    id bigint generated always as identity primary key,
    nome varchar(250) not null,
    nome_fantasia varchar(250) null,
    documento varchar(20) null,
    slug varchar(100) not null unique,
    status varchar(30) not null,
    timezone varchar(80) not null default 'America/Sao_Paulo',
    locale varchar(20) not null default 'pt-BR',
    ambiente varchar(30) not null default 'PRODUCTION',
    data_inicio_operacao timestamptz null,
    data_cancelamento timestamptz null,
    motivo_suspensao text null,
    metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.tenant_dominio (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    tipo varchar(30) not null,
    dominio varchar(250) not null unique,
    principal boolean not null default false,
    verificado boolean not null default false,
    verificado_at timestamptz null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.plano_saas (
    id bigint generated always as identity primary key,
    codigo varchar(80) unique not null,
    nome varchar(150) not null,
    descricao text null,
    valor_mensal numeric(18,2) null,
    usuarios_inclusos int null,
    entidades_inclusas int null,
    armazenamento_gb int null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.modulo_saas (
    id bigint generated always as identity primary key,
    codigo varchar(80) unique not null,
    nome varchar(150) not null,
    descricao text null,
    categoria varchar(80) null,
    ordem int not null default 0,
    rota_base varchar(150) null,
    icone varchar(80) null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.plano_modulo (
    id bigint generated always as identity primary key,
    plano_saas_id bigint not null references sigov.plano_saas(id),
    modulo_saas_id bigint not null references sigov.modulo_saas(id),
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    unique (plano_saas_id, modulo_saas_id)
);

create table if not exists sigov.tenant_assinatura (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    plano_saas_id bigint not null references sigov.plano_saas(id),
    status varchar(30) not null,
    inicio_at timestamptz not null,
    fim_at timestamptz null,
    trial_ate timestamptz null,
    vencimento_proximo_at timestamptz null,
    motivo_cancelamento text null,
    metadados jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.tenant_modulo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    modulo_saas_id bigint not null references sigov.modulo_saas(id),
    habilitado boolean not null default true,
    contratado boolean not null default true,
    inicio_at timestamptz not null default now(),
    fim_at timestamptz null,
    configuracoes jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, modulo_saas_id)
);

create table if not exists sigov.feature_flag_def (
    id bigint generated always as identity primary key,
    codigo varchar(100) unique not null,
    nome varchar(150) not null,
    descricao text null,
    modulo varchar(80) null,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null
);

create table if not exists sigov.tenant_feature_flag (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    feature_flag_def_id bigint not null references sigov.feature_flag_def(id),
    habilitado boolean not null default false,
    valor jsonb not null default '{}'::jsonb,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, feature_flag_def_id)
);

create table if not exists sigov.tenant_limite (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    chave varchar(100) not null,
    valor_limite numeric(18,4) not null,
    valor_atual numeric(18,4) not null default 0,
    unidade varchar(50) not null,
    periodo varchar(30) not null default 'MENSAL',
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, chave)
);

create table if not exists sigov.tenant_configuracao (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    chave varchar(150) not null,
    valor jsonb not null,
    secreto boolean not null default false,
    ativo boolean not null default true,
    is_deleted boolean not null default false,
    created_at timestamptz not null default now(),
    created_by bigint null,
    updated_at timestamptz null,
    updated_by bigint null,
    correlation_id uuid null,
    unique (tenant_id, chave)
);

insert into sigov.tenant (nome, nome_fantasia, slug, status, ambiente, data_inicio_operacao)
values ('Tenant de Desenvolvimento sigov', 'sigov Development', 'municipio-demo', 'ATIVO', 'DEVELOPMENT', now())
on conflict (slug) do nothing;
