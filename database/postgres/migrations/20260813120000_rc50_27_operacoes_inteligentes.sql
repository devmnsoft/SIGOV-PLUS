-- RC50.27: trilha persistente, multi-tenant e auditável para importações e alertas operacionais.
create table if not exists sigov.relatorio_importacao (
  id bigserial primary key, tenant_id bigint not null, modulo varchar(30) not null,
  recurso varchar(60) not null, total_linhas integer not null default 0,
  linhas_importadas integer not null default 0, linhas_rejeitadas integer not null default 0,
  status varchar(30) not null, detalhes jsonb not null default '{}'::jsonb,
  correlation_id varchar(100) not null, created_at timestamptz not null default now(),
  created_by bigint null, auditoria jsonb not null default '{}'::jsonb,
  is_deleted boolean not null default false
);
create index if not exists ix_relatorio_importacao_tenant on sigov.relatorio_importacao(tenant_id, modulo, recurso, created_at desc) where is_deleted=false;

create table if not exists sigov.educacao_matricula_historico (
 id bigserial primary key, tenant_id bigint not null, matricula_id bigint not null, tipo_evento varchar(40) not null,
 descricao text not null, turma_origem_id bigint null, turma_destino_id bigint null, motivo text null,
 correlation_id varchar(100) not null, created_at timestamptz not null default now(), created_by bigint null,
 auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.educacao_fila_espera (
 id bigserial primary key, tenant_id bigint not null, aluno_id bigint not null, turma_id bigint not null,
 posicao integer not null, status varchar(30) not null default 'AGUARDANDO', created_at timestamptz not null default now(),
 updated_at timestamptz null, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create unique index if not exists ux_educacao_fila_ativa on sigov.educacao_fila_espera(tenant_id,aluno_id,turma_id) where is_deleted=false and status='AGUARDANDO';
create table if not exists sigov.educacao_matricula_pendencia (
 id bigserial primary key, tenant_id bigint not null, matricula_id bigint not null, tipo varchar(50) not null,
 descricao text not null, severidade varchar(20) not null, status varchar(20) not null default 'ABERTA',
 created_at timestamptz not null default now(), resolved_at timestamptz null, resolved_by bigint null,
 auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.educacao_alerta_frequencia (
 id bigserial primary key, tenant_id bigint not null, aluno_id bigint not null, turma_id bigint not null,
 percentual numeric(5,2) not null, nivel_risco varchar(20) not null, status varchar(20) not null default 'ABERTO',
 dados jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz null,
 auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.rh_historico_funcional (
 id bigserial primary key, tenant_id bigint not null, servidor_id bigint not null, tipo_evento varchar(50) not null,
 descricao text not null, dados_anteriores jsonb not null default '{}'::jsonb, dados_novos jsonb not null default '{}'::jsonb,
 justificativa text null, correlation_id varchar(100) not null, created_at timestamptz not null default now(), created_by bigint null,
 auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.folha_simulacao (
 id bigserial primary key, tenant_id bigint not null, folha_id bigint not null, status varchar(30) not null,
 total_bruto numeric(18,2) not null default 0, total_descontos numeric(18,2) not null default 0, total_liquido numeric(18,2) not null default 0,
 comparativo jsonb not null default '{}'::jsonb, correlation_id varchar(100) not null, created_at timestamptz not null default now(),
 created_by bigint null, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.folha_simulacao_item (
 id bigserial primary key, tenant_id bigint not null, simulacao_id bigint not null references sigov.folha_simulacao(id), servidor_id bigint not null,
 bruto numeric(18,2) not null, descontos numeric(18,2) not null, liquido numeric(18,2) not null,
 dados jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.folha_simulacao_critica (
 id bigserial primary key, tenant_id bigint not null, simulacao_id bigint not null references sigov.folha_simulacao(id), tipo varchar(50) not null,
 severidade varchar(20) not null, bloqueante boolean not null default false, descricao text not null,
 status varchar(20) not null default 'ABERTA', justificativa text null, auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create table if not exists sigov.alerta_operacional (
 id bigserial primary key, tenant_id bigint not null, modulo varchar(30) not null, tipo varchar(60) not null,
 severidade varchar(20) not null, titulo varchar(180) not null, descricao text not null, referencia_tipo varchar(60) null,
 referencia_id bigint null, status varchar(20) not null default 'ABERTO', dados jsonb not null default '{}'::jsonb,
 created_at timestamptz not null default now(), updated_at timestamptz null, resolved_at timestamptz null, resolved_by bigint null,
 auditoria jsonb not null default '{}'::jsonb, is_deleted boolean not null default false
);
create index if not exists ix_alerta_operacional_aberto on sigov.alerta_operacional(tenant_id,modulo,severidade,created_at desc) where is_deleted=false and status='ABERTO';
