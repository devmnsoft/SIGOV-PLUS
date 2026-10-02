create table if not exists sigov.educacao_diario_classe (
 id bigserial primary key, tenant_id bigint not null, entidade_id bigint, escola_id bigint not null, turma_id bigint not null, disciplina_id bigint not null, professor_id bigint not null, ano_letivo_id bigint not null,
 periodo varchar(40) not null, status varchar(30) not null default 'RASCUNHO' check(status in ('RASCUNHO','ABERTO','PENDENTE','FECHADO','REABERTO','CANCELADO')), dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true, is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz,
 deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create unique index if not exists ux_educacao_diario_contexto on sigov.educacao_diario_classe(tenant_id,turma_id,disciplina_id,professor_id,periodo) where is_deleted=false;
create table if not exists sigov.educacao_diario_aula (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), data_aula date not null, carga_horaria numeric(6,2) not null check(carga_horaria>0), observacoes text,
 status varchar(30) not null default 'ABERTA', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, deleted_at timestamptz, created_by bigint, updated_by bigint, deleted_by bigint);
create table if not exists sigov.educacao_diario_conteudo (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), aula_id bigint not null references sigov.educacao_diario_aula(id), conteudo text not null check(length(trim(conteudo))>0),
 observacoes text, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_diario_frequencia (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), aula_id bigint not null references sigov.educacao_diario_aula(id), aluno_id bigint not null,
 status varchar(30) not null check(status in ('PRESENTE','FALTA','JUSTIFICADA','ABONADA')), justificativa text, dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb,
 correlation_id varchar(80), is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create unique index if not exists ux_diario_frequencia_aula_aluno on sigov.educacao_diario_frequencia(tenant_id,aula_id,aluno_id) where is_deleted=false;
create table if not exists sigov.educacao_diario_avaliacao (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), aula_id bigint, titulo varchar(180) not null, valor_maximo numeric(7,2) not null, peso numeric(7,2) not null default 1,
 status varchar(30) not null default 'ABERTA', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_diario_reposicao (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), aula_id bigint not null, data_reposicao date not null, justificativa text not null,
 status varchar(30) not null default 'PROGRAMADA', dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_diario_fechamento (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), periodo varchar(40) not null, status varchar(30) not null, justificativa text,
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), created_at timestamptz not null default now(), created_by bigint);
create table if not exists sigov.educacao_diario_pendencia (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), tipo varchar(60) not null, descricao text not null, status varchar(30) not null default 'PENDENTE',
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), is_deleted boolean not null default false, created_at timestamptz not null default now(), updated_at timestamptz, created_by bigint, updated_by bigint);
create table if not exists sigov.educacao_diario_historico (
 id bigserial primary key, tenant_id bigint not null, diario_id bigint not null references sigov.educacao_diario_classe(id), status varchar(30) not null, justificativa text not null,
 auditoria jsonb not null default '{}'::jsonb, correlation_id varchar(80), created_at timestamptz not null default now(), created_by bigint);
create index if not exists ix_diario_tenant_status on sigov.educacao_diario_classe(tenant_id,status) where is_deleted=false;
create index if not exists ix_diario_aula_data on sigov.educacao_diario_aula(tenant_id,data_aula) where is_deleted=false;
