-- RC50.51: governanca transversal. Estruturas incrementais, sem descarte destrutivo.
set search_path to sigov;

create table if not exists sigov.seguranca_recurso (
    id bigint generated always as identity primary key, tenant_id bigint null,
    modulo varchar(80) not null, codigo varchar(120) not null, nome varchar(180) not null,
    entidade varchar(120) null, ativo boolean not null default true,
    created_at timestamptz not null default now(), updated_at timestamptz null,
    unique nulls not distinct (tenant_id, modulo, codigo)
);
create table if not exists sigov.seguranca_permissao_granular (
    id bigint generated always as identity primary key, tenant_id bigint null,
    recurso_id bigint not null references sigov.seguranca_recurso(id), acao varchar(40) not null,
    escopo varchar(40) not null default 'TENANT', entidade_id bigint null, ativo boolean not null default true,
    created_at timestamptz not null default now(), updated_at timestamptz null,
    unique nulls not distinct (tenant_id, recurso_id, acao, escopo, entidade_id)
);
create table if not exists sigov.seguranca_perfil_permissao (
    id bigint generated always as identity primary key, tenant_id bigint not null,
    perfil_id bigint not null, permissao_id bigint not null references sigov.seguranca_permissao_granular(id),
    concedida boolean not null default true, created_by bigint null, created_at timestamptz not null default now(),
    unique (tenant_id, perfil_id, permissao_id)
);
create table if not exists sigov.seguranca_usuario_permissao (
    id bigint generated always as identity primary key, tenant_id bigint not null,
    usuario_id bigint not null, permissao_id bigint not null references sigov.seguranca_permissao_granular(id),
    concedida boolean not null, motivo varchar(500) null, expira_em timestamptz null,
    created_by bigint null, created_at timestamptz not null default now(),
    unique (tenant_id, usuario_id, permissao_id)
);
create table if not exists sigov.seguranca_restricao_acesso (
    id bigint generated always as identity primary key, tenant_id bigint not null,
    usuario_id bigint null, perfil_id bigint null, modulo varchar(80) not null,
    recurso varchar(120) null, entidade_id bigint null, tipo varchar(40) not null,
    motivo varchar(500) not null, ativo boolean not null default true,
    created_by bigint null, created_at timestamptz not null default now()
);
create table if not exists sigov.seguranca_evento (
    id bigint generated always as identity primary key, tenant_id bigint null, usuario_id bigint null,
    modulo varchar(80) not null, recurso varchar(120) not null, acao varchar(40) not null,
    permitido boolean not null, entidade_id bigint null, motivo varchar(500) null,
    ip inet null, user_agent varchar(500) null, correlation_id varchar(100) not null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.lgpd_consentimento_governanca (
    id bigint generated always as identity primary key, tenant_id bigint not null, titular_id bigint not null,
    finalidade varchar(250) not null, base_legal varchar(120) not null, canal varchar(60) not null,
    concedido_em timestamptz not null, revogado_em timestamptz null, evidencia jsonb not null default '{}'::jsonb,
    correlation_id varchar(100) not null, created_at timestamptz not null default now()
);
create table if not exists sigov.lgpd_incidente (
    id bigint generated always as identity primary key, tenant_id bigint not null,
    severidade varchar(20) not null check (severidade in ('BAIXA','MEDIA','ALTA','CRITICA')),
    descoberto_em timestamptz not null, descricao text not null, status varchar(30) not null default 'ABERTO',
    dados_afetados jsonb not null default '[]'::jsonb, plano_acao text null, encerrado_em timestamptz null,
    created_by bigint null, correlation_id varchar(100) not null, created_at timestamptz not null default now()
);
create table if not exists sigov.lgpd_retencao_politica (
    id bigint generated always as identity primary key, tenant_id bigint not null, recurso varchar(120) not null,
    base_legal varchar(120) not null, prazo_dias integer not null check (prazo_dias > 0),
    destino varchar(30) not null check (destino in ('REVISAR','ANONIMIZAR','DESCARTAR_PREPARATORIO')),
    ativo boolean not null default true, approved_by bigint null, created_at timestamptz not null default now(),
    unique (tenant_id, recurso)
);
create table if not exists sigov.lgpd_acesso_dado_pessoal (
    id bigint generated always as identity primary key, tenant_id bigint not null, usuario_id bigint null,
    titular_id bigint null, modulo varchar(80) not null, recurso varchar(120) not null,
    finalidade varchar(250) not null, base_legal varchar(120) null, operacao varchar(30) not null,
    campos jsonb not null default '[]'::jsonb, exportacao boolean not null default false,
    ip inet null, user_agent varchar(500) null, correlation_id varchar(100) not null,
    created_at timestamptz not null default now()
);

create table if not exists sigov.auditoria_evento_operacional (
    id bigint generated always as identity primary key, tenant_id bigint null, usuario_id bigint null,
    modulo varchar(80) not null, recurso varchar(120) not null, acao varchar(40) not null,
    entidade varchar(120) null, entidade_id varchar(100) null, dados_antes jsonb null, dados_depois jsonb null,
    severidade varchar(20) not null default 'INFO', ip inet null, user_agent varchar(500) null,
    correlation_id varchar(100) not null, created_at timestamptz not null default now()
);
create table if not exists sigov.auditoria_exportacao (
    id bigint generated always as identity primary key, tenant_id bigint not null, usuario_id bigint null,
    modulo varchar(80) not null, recurso varchar(120) not null, finalidade varchar(250) not null,
    formato varchar(20) not null, quantidade_registros integer not null default 0,
    campos_mascarados boolean not null default true, correlation_id varchar(100) not null,
    created_at timestamptz not null default now()
);

create index if not exists ix_seguranca_evento_tenant_data on sigov.seguranca_evento (tenant_id, created_at desc);
create index if not exists ix_lgpd_incidente_tenant_status on sigov.lgpd_incidente (tenant_id, status, descoberto_em desc);
create index if not exists ix_lgpd_acesso_tenant_data on sigov.lgpd_acesso_dado_pessoal (tenant_id, created_at desc);
create index if not exists ix_auditoria_evento_tenant_data on sigov.auditoria_evento_operacional (tenant_id, created_at desc);

insert into sigov.seguranca_recurso(modulo,codigo,nome)
values ('GOVERNANCA','SEGURANCA','Segurança e permissões'),('GOVERNANCA','LGPD','Governança LGPD'),
       ('GOVERNANCA','AUDITORIA','Auditoria operacional'),('GOVERNANCA','OBSERVABILIDADE','Observabilidade')
on conflict do nothing;
insert into sigov.seguranca_permissao_granular(recurso_id,acao)
select r.id, a.acao from sigov.seguranca_recurso r
cross join (values ('visualizar'),('criar'),('alterar'),('excluir'),('aprovar'),('exportar'),('administrar')) a(acao)
where r.modulo='GOVERNANCA' on conflict do nothing;
