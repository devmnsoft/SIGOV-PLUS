-- RC50.28 - gestão avançada. PostgreSQL, Database=postgres, schema sigov.
create table if not exists sigov.workflow_operacional (
 id bigserial primary key, tenant_id bigint not null, modulo varchar(30) not null, tipo_fluxo varchar(80) not null,
 nome varchar(160) not null, status varchar(30) not null default 'ATIVO', dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz null,
 created_by bigint null, updated_by bigint null, is_deleted boolean not null default false);
create unique index if not exists ux_workflow_operacional_tipo on sigov.workflow_operacional(tenant_id,modulo,tipo_fluxo) where is_deleted=false;
create table if not exists sigov.workflow_etapa (
 id bigserial primary key, tenant_id bigint not null, workflow_id bigint not null references sigov.workflow_operacional(id), codigo varchar(60) not null,
 nome varchar(160) not null, ordem integer not null, grupo_responsavel varchar(80) null, prazo_horas integer null, dados jsonb not null default '{}'::jsonb,
 auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz null, created_by bigint null, updated_by bigint null, is_deleted boolean not null default false);
create unique index if not exists ux_workflow_etapa_ordem on sigov.workflow_etapa(tenant_id,workflow_id,ordem) where is_deleted=false;
create table if not exists sigov.workflow_instancia (
 id bigserial primary key, tenant_id bigint not null, workflow_id bigint null references sigov.workflow_operacional(id), modulo varchar(30) not null,
 tipo_fluxo varchar(80) not null, referencia_tipo varchar(80) not null, referencia_id bigint not null, status varchar(30) not null default 'PENDENTE',
 etapa_atual varchar(60) not null, responsavel_id bigint null, grupo_responsavel varchar(80) null, prazo timestamptz null, prioridade varchar(20) not null default 'NORMAL',
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz null,
 created_by bigint null, updated_by bigint null, is_deleted boolean not null default false);
create index if not exists ix_workflow_instancia_referencia on sigov.workflow_instancia(tenant_id,referencia_tipo,referencia_id) where is_deleted=false;
create table if not exists sigov.workflow_tarefa (
 id bigserial primary key, tenant_id bigint not null, instancia_id bigint not null references sigov.workflow_instancia(id), status varchar(30) not null default 'PENDENTE',
 etapa_atual varchar(60) not null, responsavel_id bigint null, grupo_responsavel varchar(80) null, prazo timestamptz null, prioridade varchar(20) not null default 'NORMAL',
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz null,
 created_by bigint null, updated_by bigint null, is_deleted boolean not null default false);
create index if not exists ix_workflow_tarefa_caixa on sigov.workflow_tarefa(tenant_id,responsavel_id,status,prazo) where is_deleted=false;
create table if not exists sigov.workflow_historico (
 id bigserial primary key, tenant_id bigint not null, instancia_id bigint not null references sigov.workflow_instancia(id), decisao varchar(30) not null,
 justificativa text null, etapa_anterior varchar(60) not null, etapa_nova varchar(60) not null, usuario_id bigint null, correlation_id varchar(100) not null,
 auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), is_deleted boolean not null default false);
create table if not exists sigov.workflow_notificacao (
 id bigserial primary key, tenant_id bigint not null, instancia_id bigint not null references sigov.workflow_instancia(id), tarefa_id bigint null references sigov.workflow_tarefa(id),
 destinatario_id bigint null, canal varchar(30) not null default 'INTERNO', titulo varchar(180) not null, mensagem text not null, status varchar(30) not null default 'PENDENTE',
 dados jsonb not null default '{}'::jsonb, auditoria jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), updated_at timestamptz null,
 created_by bigint null, updated_by bigint null, is_deleted boolean not null default false);

-- Cada recurso mantém isolamento, auditoria e soft delete. Campos específicos ficam em dados até a estabilização do contrato.
do $migration$
declare table_name text;
begin
 foreach table_name in array array[
  'educacao_documento_escolar','educacao_solicitacao_escolar','educacao_ocorrencia_escolar','educacao_atendimento_responsavel','educacao_transferencia',
  'educacao_diario_classe','educacao_diario_aula','educacao_diario_conteudo','educacao_diario_pendencia','educacao_conselho_classe','educacao_conselho_aluno',
  'educacao_intervencao_pedagogica','educacao_plano_recuperacao','educacao_portal_usuario','educacao_portal_acesso','educacao_portal_solicitacao',
  'rh_carreira','rh_classe_referencia','rh_progressao','rh_dependente','rh_beneficio','rh_servidor_beneficio','folha_status_historico',
  'folha_evento_automatico','folha_regra_evento','folha_evento_origem','rh_consignacao','rh_margem_consignavel','rh_consignacao_lancamento',
  'folha_remessa_pagamento','folha_remessa_pagamento_item','esocial_evento_preparacao','esocial_pendencia','esocial_lote_preparacao']
 loop
  execute format('create table if not exists sigov.%I (
   id bigserial primary key, tenant_id bigint not null, referencia_id bigint null, referencia_tipo varchar(80) null,
   status varchar(30) not null default ''ATIVO'', dados jsonb not null default ''{}''::jsonb, auditoria jsonb not null default ''{}''::jsonb,
   created_at timestamptz not null default now(), updated_at timestamptz null, created_by bigint null, updated_by bigint null,
   is_deleted boolean not null default false)', table_name);
  execute format('create index if not exists %I on sigov.%I(tenant_id,status,created_at desc) where is_deleted=false', 'ix_'||table_name||'_tenant', table_name);
 end loop;
end $migration$;

-- Regras estruturais críticas, idempotentes.
alter table sigov.educacao_diario_classe add column if not exists professor_id bigint null;
alter table sigov.educacao_diario_classe add column if not exists turma_id bigint null;
alter table sigov.educacao_diario_classe add column if not exists disciplina_id bigint null;
alter table sigov.educacao_diario_classe add column if not exists periodo varchar(30) null;
create unique index if not exists ux_diario_turma_disciplina_periodo on sigov.educacao_diario_classe(tenant_id,turma_id,disciplina_id,periodo) where is_deleted=false;
alter table sigov.folha_status_historico add column if not exists folha_id bigint null;
alter table sigov.folha_status_historico add column if not exists status_anterior varchar(30) null;
alter table sigov.folha_status_historico add column if not exists status_novo varchar(30) null;
alter table sigov.folha_status_historico add column if not exists justificativa text null;
alter table sigov.folha_remessa_pagamento add column if not exists folha_id bigint null;
alter table sigov.folha_remessa_pagamento add column if not exists valor_total numeric(18,2) not null default 0;
alter table sigov.folha_remessa_pagamento_item add column if not exists remessa_id bigint null;
alter table sigov.folha_remessa_pagamento_item add column if not exists valor_liquido numeric(18,2) not null default 0;
alter table sigov.rh_consignacao add column if not exists valor_mensal numeric(18,2) not null default 0;
alter table sigov.rh_dependente add column if not exists documento_mascarado varchar(30) null;
