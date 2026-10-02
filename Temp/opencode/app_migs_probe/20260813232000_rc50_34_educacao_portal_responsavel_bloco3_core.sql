create table if not exists sigov.educacao_portal_usuario (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, tipo varchar(30) not null, status varchar(30) not null default 'ATIVO', dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create unique index if not exists ux_portal_usuario on sigov.educacao_portal_usuario(tenant_id,usuario_id) where is_deleted=false;
create table if not exists sigov.educacao_portal_vinculo (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, aluno_id bigint not null, responsavel_id bigint, tipo varchar(30) not null default 'RESPONSAVEL', status varchar(30) not null default 'ATIVO',
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create unique index if not exists ux_portal_vinculo on sigov.educacao_portal_vinculo(tenant_id,usuario_id,aluno_id) where is_deleted=false;
create table if not exists sigov.educacao_portal_acesso (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, aluno_id bigint, tipo varchar(60) not null, dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), created_at timestamptz not null default now());
create table if not exists sigov.educacao_portal_solicitacao (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, aluno_id bigint not null, responsavel_id bigint, tipo varchar(40) not null check(tipo in ('DECLARACAO','TRANSFERENCIA','ATUALIZACAO_CADASTRAL','JUSTIFICATIVA_FALTA','REUNIAO','OUTROS')),
 status varchar(30) not null default 'ABERTA' check(status in ('ABERTA','EM_ANALISE','RESPONDIDA','CONCLUIDA','CANCELADA')), titulo varchar(180), descricao text not null, resposta text,
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_portal_solicitacao_historico (
 id bigserial primary key, tenant_id bigint not null, solicitacao_id bigint not null references sigov.educacao_portal_solicitacao(id), status varchar(30) not null, descricao text not null,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), created_at timestamptz not null default now(), created_by bigint);
create table if not exists sigov.educacao_comunicado (
 id bigserial primary key, tenant_id bigint not null, escola_id bigint, turma_id bigint, tipo varchar(40) not null default 'GERAL', status varchar(30) not null default 'PUBLICADO', titulo varchar(180) not null, mensagem text not null,
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_comunicado_destinatario (
 id bigserial primary key, tenant_id bigint not null, comunicado_id bigint not null references sigov.educacao_comunicado(id), usuario_id bigint, aluno_id bigint, lido_at timestamptz,
 dados jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), created_by bigint);
create table if not exists sigov.educacao_portal_mensagem (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, aluno_id bigint, tipo varchar(40) not null default 'INFORMATIVA', status varchar(30) not null default 'NAO_LIDA', titulo varchar(180) not null, mensagem text not null,
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_portal_preferencia (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, tipo varchar(60) not null, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create index if not exists ix_portal_solicitacao_usuario on sigov.educacao_portal_solicitacao(tenant_id,usuario_id,status) where is_deleted=false;
create index if not exists ix_portal_mensagem_usuario on sigov.educacao_portal_mensagem(tenant_id,usuario_id,status) where is_deleted=false;
