create schema if not exists sigov;

create table if not exists sigov.educacao_documento_modelo (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, exercicio_id bigint, tipo varchar(60) not null, titulo varchar(180) not null,
 html_modelo text not null, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_documento_escolar (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, exercicio_id bigint, escola_id bigint, aluno_id bigint not null, matricula_id bigint not null,
 tipo varchar(60) not null, status varchar(30) not null default 'EMITIDO', titulo varchar(180) not null, descricao text, html_emitido text, dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true, is_deleted boolean not null default false,
 created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_solicitacao_escolar (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, exercicio_id bigint, escola_id bigint, aluno_id bigint not null, matricula_id bigint, responsavel_id bigint,
 tipo varchar(60) not null, status varchar(30) not null default 'ABERTA' check (status in ('ABERTA','EM_ANALISE','DEFERIDA','INDEFERIDA','CONCLUIDA','CANCELADA')),
 titulo varchar(180), descricao text not null, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_solicitacao_historico (
 id bigserial primary key, tenant_id bigint not null, solicitacao_id bigint not null references sigov.educacao_solicitacao_escolar(id), status varchar(30) not null, justificativa text not null,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), created_at timestamptz not null default now(), created_by bigint);
create table if not exists sigov.educacao_ocorrencia_escolar (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, exercicio_id bigint, escola_id bigint, aluno_id bigint not null, matricula_id bigint, tipo varchar(60) not null,
 status varchar(30) not null default 'REGISTRADA', titulo varchar(180), descricao text not null, data_ocorrencia timestamptz not null, visivel_portal boolean not null default false,
 sensivel boolean not null default false, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_atendimento_responsavel (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, escola_id bigint, aluno_id bigint, responsavel_id bigint not null, tipo varchar(60) not null, status varchar(30) not null default 'ABERTO',
 titulo varchar(180), descricao text not null, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_transferencia (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, exercicio_id bigint, escola_id bigint, aluno_id bigint not null, matricula_id bigint not null, escola_destino_id bigint,
 turma_destino_id bigint, justificativa_externa text, tipo varchar(60) not null default 'TRANSFERENCIA', status varchar(30) not null default 'SOLICITADA' check(status in ('SOLICITADA','EM_ANALISE','APROVADA','REPROVADA','CONCLUIDA','CANCELADA')),
 titulo varchar(180), descricao text not null, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_pendencia_documental (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, escola_id bigint, aluno_id bigint not null, matricula_id bigint, tipo varchar(60) not null, status varchar(30) not null default 'PENDENTE',
 titulo varchar(180), descricao text not null, data_vencimento timestamptz, resolvido_at timestamptz, resolvido_by bigint, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb,
 correlation_id varchar(80), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz,
 created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_historico_escolar (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, escola_id bigint, aluno_id bigint not null, matricula_id bigint, status varchar(30) not null default 'ABERTO', dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz,
 deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_historico_escolar_item (
 id bigserial primary key, tenant_id bigint not null, historico_id bigint not null references sigov.educacao_historico_escolar(id), componente_curricular varchar(160) not null, nota numeric(7,2), frequencia numeric(7,2),
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), created_by bigint);
create table if not exists sigov.educacao_secretaria_evento (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, tipo varchar(80) not null, agregado varchar(80) not null, agregado_id bigint not null, dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), created_at timestamptz not null default now(), created_by bigint);

do $$ declare t text; begin foreach t in array array['educacao_documento_escolar','educacao_solicitacao_escolar','educacao_ocorrencia_escolar','educacao_transferencia','educacao_pendencia_documental'] loop
 execute format('create index if not exists ix_%s_tenant_deleted on sigov.%I(tenant_id,is_deleted)', t, t);
 execute format('create index if not exists ix_%s_aluno on sigov.%I(tenant_id,aluno_id)', t, t);
 execute format('create index if not exists ix_%s_status on sigov.%I(tenant_id,status)', t, t);
 execute format('create index if not exists ix_%s_created on sigov.%I(tenant_id,created_at desc)', t, t);
end loop; end $$;
