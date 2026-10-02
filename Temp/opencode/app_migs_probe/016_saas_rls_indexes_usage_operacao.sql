create or replace function sigov.current_tenant_id()
returns bigint
language sql
stable
as $$
    select nullif(current_setting('sigov.tenant_id', true), '')::bigint;
$$;

do $$
declare
    v_table_name text;
    rls_tables text[] := array['pessoa','usuario','chamado','solicitacao_titular','trilha_auditoria','acesso_dado_pessoal'];
begin
    foreach v_table_name in array rls_tables loop
        if exists (select 1 from information_schema.tables where table_schema = 'sigov' and table_name = v_table_name)
           and exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = v_table_name and column_name = 'tenant_id') then
            execute format('alter table sigov.%I enable row level security', v_table_name);
            execute format('drop policy if exists %I on sigov.%I', 'rls_' || v_table_name || '_tenant_isolation', v_table_name);
            execute format('create policy %I on sigov.%I using (tenant_id = sigov.current_tenant_id()) with check (tenant_id = sigov.current_tenant_id())', 'rls_' || v_table_name || '_tenant_isolation', v_table_name);
        end if;
    end loop;
end $$;

create table if not exists sigov.tenant_uso_mensal (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    ano int not null,
    mes int not null,
    usuarios_ativos int not null default 0,
    requisicoes_api bigint not null default 0,
    armazenamento_bytes bigint not null default 0,
    chamados_abertos int not null default 0,
    logins int not null default 0,
    eventos_processados bigint not null default 0,
    metadados jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz null,
    unique (tenant_id, ano, mes)
);

create table if not exists sigov.tenant_evento_operacional (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    tipo varchar(80) not null,
    severidade varchar(30) not null,
    mensagem text not null,
    payload jsonb not null default '{}'::jsonb,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.job_execucao (
    id bigint generated always as identity primary key,
    job_nome varchar(150) not null,
    tenant_id bigint null references sigov.tenant(id),
    status varchar(30) not null,
    iniciou_at timestamptz not null default now(),
    finalizou_at timestamptz null,
    duracao_ms bigint null,
    erro text null,
    metadados jsonb not null default '{}'::jsonb
);

create table if not exists sigov.health_check_historico (
    id bigint generated always as identity primary key,
    nome varchar(100) not null,
    status varchar(30) not null,
    duracao_ms bigint null,
    detalhes jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create table if not exists sigov.metric_snapshot (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    nome varchar(100) not null,
    valor numeric(18,4) not null,
    tags jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create table if not exists sigov.idempotency_key (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    chave varchar(150) not null,
    metodo varchar(20) not null,
    rota varchar(250) not null,
    request_hash varchar(128) null,
    response_hash varchar(128) null,
    status varchar(30) not null,
    expires_at timestamptz not null,
    created_at timestamptz not null default now(),
    unique (tenant_id, chave)
);

create table if not exists sigov.evento_seguranca (
    id bigint generated always as identity primary key,
    tenant_id bigint null references sigov.tenant(id),
    usuario_id bigint null,
    tipo varchar(80) not null,
    severidade varchar(30) not null,
    ip varchar(80) null,
    user_agent text null,
    detalhes jsonb not null default '{}'::jsonb,
    correlation_id uuid null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.backup_execucao (
    id bigint generated always as identity primary key,
    ambiente varchar(30) not null,
    arquivo text not null,
    tamanho_bytes bigint null,
    checksum varchar(128) null,
    status varchar(30) not null,
    iniciou_at timestamptz not null default now(),
    finalizou_at timestamptz null,
    erro text null,
    metadados jsonb not null default '{}'::jsonb
);

create table if not exists sigov.restore_execucao (
    id bigint generated always as identity primary key,
    ambiente varchar(30) not null,
    arquivo text not null,
    status varchar(30) not null,
    iniciou_at timestamptz not null default now(),
    finalizou_at timestamptz null,
    erro text null,
    metadados jsonb not null default '{}'::jsonb
);

create table if not exists sigov.arquivo (
    id bigint generated always as identity primary key,
    tenant_id bigint not null references sigov.tenant(id),
    entidade_id bigint null,
    nome_original varchar(250) not null,
    content_type varchar(150) not null,
    tamanho_bytes bigint not null,
    hash_sha256 varchar(128) not null,
    storage_provider varchar(50) not null,
    storage_key text not null,
    categoria varchar(80) null,
    modulo varchar(80) null,
    tabela_origem varchar(150) null,
    chave_origem varchar(150) null,
    status varchar(30) not null,
    criado_por bigint null,
    created_at timestamptz not null default now(),
    is_deleted boolean not null default false
);

create index if not exists idx_tenant_slug on sigov.tenant (slug);
create index if not exists idx_tenant_status on sigov.tenant (status);
create index if not exists idx_tenant_dominio_dominio on sigov.tenant_dominio (dominio);
create index if not exists idx_tenant_assinatura_status on sigov.tenant_assinatura (status);
create index if not exists idx_tenant_modulo_tenant_modulo on sigov.tenant_modulo (tenant_id, modulo_saas_id);
