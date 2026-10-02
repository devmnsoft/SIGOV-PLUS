-- RC50.30: núcleo executivo interno. Compatível com Database=postgres e schema sigov.
set search_path to sigov, public;

create table if not exists sigov.notificacao_interna (
 id bigserial primary key, tenant_id bigint not null, modulo varchar(40) not null, tipo varchar(80) not null,
 titulo varchar(160) not null, mensagem varchar(500) not null, severidade varchar(20) not null default 'INFORMATIVA',
 referencia_tipo varchar(80), referencia_id bigint, url_destino varchar(500), dados jsonb not null default '{}'::jsonb,
 created_at timestamptz not null default now(), created_by bigint, is_deleted boolean not null default false,
 constraint ck_notificacao_sem_dado_sensivel check (titulo !~ '[0-9]{11}' and mensagem !~ '[0-9]{11}')
);
create table if not exists sigov.notificacao_destinatario (
 notificacao_id bigint not null references sigov.notificacao_interna(id), usuario_id bigint not null,
 lida boolean not null default false, lida_at timestamptz, arquivada boolean not null default false, arquivada_at timestamptz,
 primary key(notificacao_id,usuario_id)
);
create table if not exists sigov.notificacao_preferencia (
 tenant_id bigint not null, usuario_id bigint not null, preferencias jsonb not null default '{}'::jsonb,
 created_at timestamptz not null default now(), updated_at timestamptz not null default now(), primary key(tenant_id,usuario_id)
);
create table if not exists sigov.notificacao_historico (
 id bigserial primary key, tenant_id bigint not null, notificacao_id bigint not null references sigov.notificacao_interna(id),
 usuario_id bigint, acao varchar(40) not null, correlation_id uuid, dados jsonb not null default '{}'::jsonb, created_at timestamptz not null default now()
);
create table if not exists sigov.integracao_interna_evento (
 id bigserial primary key, tenant_id bigint not null, origem_modulo varchar(40) not null, destino_modulo varchar(40) not null,
 tipo_evento varchar(100) not null, status varchar(30) not null default 'PENDENTE', referencia_tipo varchar(80), referencia_id bigint,
 payload jsonb not null default '{}'::jsonb, erro text, correlation_id uuid not null default gen_random_uuid(),
 created_at timestamptz not null default now(), processed_at timestamptz, created_by bigint, is_deleted boolean not null default false,
 auditoria jsonb not null default '{}'::jsonb
);
create table if not exists sigov.qualidade_dados_inconsistencia (
 id bigserial primary key, tenant_id bigint not null, modulo varchar(40) not null, tipo varchar(100) not null,
 severidade varchar(20) not null, descricao varchar(500) not null, referencia_tipo varchar(80), referencia_id bigint,
 status varchar(30) not null default 'ABERTA', dados jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(),
 resolved_at timestamptz, resolved_by bigint, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.assistente_operacional_execucao (
 id bigserial primary key, tenant_id bigint not null, usuario_id bigint not null, assistente varchar(80) not null,
 etapa varchar(80) not null, status varchar(30) not null default 'EM_ANDAMENTO', dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz not null default now(), is_deleted boolean not null default false
);

create index if not exists ix_notificacao_tenant_created on sigov.notificacao_interna(tenant_id,created_at desc,id desc) where not is_deleted;
create index if not exists ix_notificacao_dest_usuario_estado on sigov.notificacao_destinatario(usuario_id,lida,arquivada,notificacao_id);
create index if not exists ix_integracao_interna_fila on sigov.integracao_interna_evento(tenant_id,status,created_at,id) where not is_deleted;
create index if not exists ix_qualidade_abertas on sigov.qualidade_dados_inconsistencia(tenant_id,modulo,severidade,created_at desc,id desc) where status='ABERTA' and not is_deleted;
create index if not exists ix_assistente_usuario on sigov.assistente_operacional_execucao(tenant_id,usuario_id,assistente,updated_at desc) where not is_deleted;
