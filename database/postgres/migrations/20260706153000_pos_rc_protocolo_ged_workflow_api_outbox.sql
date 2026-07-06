-- Pós-RC homologação real: API v1, Protocolo, GED, Workflow, Outbox e Webhooks.
-- Idempotente e não destrutiva.
create schema if not exists sigov;

create table if not exists sigov.api_key (id bigserial primary key, tenant_id bigint not null, nome varchar(160) not null, prefixo varchar(32) not null, api_key_hash varchar(128) not null, algoritmo_hash varchar(40) not null default 'SHA-256', status varchar(30) not null default 'ATIVA', dados_json jsonb null, last_used_at timestamptz null, revoked_at timestamptz null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null);
create table if not exists sigov.api_key_escopo (id bigserial primary key, tenant_id bigint not null, api_key_id bigint not null, escopo varchar(80) not null, status varchar(30) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null);
create table if not exists sigov.api_requisicao_log (id bigserial primary key, tenant_id bigint null, api_key_id bigint null, endpoint varchar(300) not null, method varchar(12) not null, status varchar(30) not null default 'REGISTRADA', status_code int not null, correlation_id uuid null, ip varchar(80) null, user_agent varchar(500) null, started_at timestamptz not null default now(), elapsed_ms bigint not null default 0, dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false);
create table if not exists sigov.webhook_configuracao (id bigserial primary key, tenant_id bigint not null, nome varchar(160) not null, url text not null, secret_hash varchar(128) not null, eventos jsonb not null default '[]'::jsonb, status varchar(30) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null);
create table if not exists sigov.webhook_entrega (id bigserial primary key, tenant_id bigint not null, webhook_configuracao_id bigint null, outbox_evento_id bigint null, evento varchar(120) not null, endpoint text not null, status varchar(30) not null default 'PENDENTE', http_status int null, tentativa int not null default 0, assinatura_prefixo varchar(24) null, erro_mascarado text null, payload_mascarado jsonb null, dados_json jsonb null, delivered_at timestamptz null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null);
create table if not exists sigov.outbox_evento (id bigserial primary key, tenant_id bigint not null, evento varchar(120) not null, agregado varchar(120) null, agregado_id bigint null, payload jsonb not null default '{}'::jsonb, status varchar(30) not null default 'PENDENTE', tentativas int not null default 0, proxima_tentativa_at timestamptz null, erro_mascarado text null, dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null);

create table if not exists sigov.protocolo (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, exercicio int not null default extract(year from now()), assunto varchar(250) not null default 'Sem assunto');

create table if not exists sigov.protocolo_movimento (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, protocolo_id bigint not null, observacao text null);

create table if not exists sigov.protocolo_anexo (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, protocolo_id bigint not null, documento_id bigint not null);

create table if not exists sigov.documento (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, titulo varchar(250) not null default 'Documento', hash_sha256 varchar(64) null, storage_path text null, classificacao_lgpd varchar(40) not null default 'PUBLICO');

create table if not exists sigov.documento_versao (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, documento_id bigint not null, versao int not null default 1, hash_sha256 varchar(64) null, storage_path text null);

create table if not exists sigov.ged_pasta (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, nome varchar(180) not null default 'Geral', pasta_pai_id bigint null);

create table if not exists sigov.workflow (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null);

create table if not exists sigov.workflow_etapa (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, workflow_id bigint not null, nome varchar(180) not null default 'Etapa');

create table if not exists sigov.workflow_transicao (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, workflow_id bigint not null, etapa_origem_id bigint null, etapa_destino_id bigint not null);

create table if not exists sigov.workflow_instancia (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, workflow_id bigint null, protocolo_id bigint null, etapa_atual_id bigint null);

create table if not exists sigov.workflow_historico (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, workflow_instancia_id bigint not null, etapa_origem_id bigint null, etapa_destino_id bigint null);

create table if not exists sigov.tarefa (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, protocolo_id bigint null, workflow_instancia_id bigint null, titulo varchar(220) not null default 'Tarefa', responsavel_id bigint null, concluida_at timestamptz null);

create table if not exists sigov.notificacao (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, titulo varchar(220) not null default 'Notificação', mensagem text null);

create table if not exists sigov.notificacao_usuario (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, notificacao_id bigint not null, usuario_id bigint not null, lida_at timestamptz null);

create table if not exists sigov.portal_validacao_documento (id bigserial primary key, tenant_id bigint not null, numero varchar(80) null, codigo varchar(80) null, status varchar(40) not null default 'ATIVO', dados_json jsonb null, created_at timestamptz not null default now(), created_by bigint null, updated_at timestamptz null, updated_by bigint null, deleted_at timestamptz null, deleted_by bigint null, is_deleted boolean not null default false, correlation_id uuid null, documento_id bigint not null, codigo_publico varchar(120) not null, hash_publico varchar(128) not null, valido_ate timestamptz null);
-- Complements for legacy tables when they already exist.
alter table if exists sigov.api_key add column if not exists tenant_id bigint;
alter table if exists sigov.api_key add column if not exists api_key_hash varchar(128);
alter table if exists sigov.api_key add column if not exists prefixo varchar(32);
alter table if exists sigov.api_key add column if not exists status varchar(30) not null default 'ATIVA';
alter table if exists sigov.api_key add column if not exists is_deleted boolean not null default false;
alter table if exists sigov.api_key_escopo add column if not exists escopo varchar(80);
alter table if exists sigov.api_requisicao_log add column if not exists status varchar(30) not null default 'REGISTRADA';
alter table if exists sigov.outbox_evento add column if not exists status varchar(30) not null default 'PENDENTE';
alter table if exists sigov.protocolo add column if not exists tenant_id bigint;
alter table if exists sigov.documento add column if not exists tenant_id bigint;
alter table if exists sigov.workflow_instancia add column if not exists tenant_id bigint;

create index if not exists ix_api_key_tenant_id on sigov.api_key (tenant_id);
create index if not exists ix_api_key_status on sigov.api_key (status);
create index if not exists ix_api_key_created_at on sigov.api_key (created_at);
create index if not exists ix_api_key_escopo_tenant_id on sigov.api_key_escopo (tenant_id);
create index if not exists ix_api_key_escopo_status on sigov.api_key_escopo (status);
create index if not exists ix_api_key_escopo_created_at on sigov.api_key_escopo (created_at);
create index if not exists ix_api_requisicao_log_tenant_id on sigov.api_requisicao_log (tenant_id);
create index if not exists ix_api_requisicao_log_status on sigov.api_requisicao_log (status);
create index if not exists ix_api_requisicao_log_created_at on sigov.api_requisicao_log (created_at);
create index if not exists ix_webhook_configuracao_tenant_id on sigov.webhook_configuracao (tenant_id);
create index if not exists ix_webhook_configuracao_status on sigov.webhook_configuracao (status);
create index if not exists ix_webhook_configuracao_created_at on sigov.webhook_configuracao (created_at);
create index if not exists ix_webhook_entrega_tenant_id on sigov.webhook_entrega (tenant_id);
create index if not exists ix_webhook_entrega_status on sigov.webhook_entrega (status);
create index if not exists ix_webhook_entrega_created_at on sigov.webhook_entrega (created_at);
create index if not exists ix_outbox_evento_tenant_id on sigov.outbox_evento (tenant_id);
create index if not exists ix_outbox_evento_status on sigov.outbox_evento (status);
create index if not exists ix_outbox_evento_created_at on sigov.outbox_evento (created_at);
create index if not exists ix_protocolo_tenant_id on sigov.protocolo (tenant_id);
create index if not exists ix_protocolo_status on sigov.protocolo (status);
create index if not exists ix_protocolo_created_at on sigov.protocolo (created_at);
create index if not exists ix_protocolo_movimento_tenant_id on sigov.protocolo_movimento (tenant_id);
create index if not exists ix_protocolo_movimento_status on sigov.protocolo_movimento (status);
create index if not exists ix_protocolo_movimento_created_at on sigov.protocolo_movimento (created_at);
create index if not exists ix_protocolo_anexo_tenant_id on sigov.protocolo_anexo (tenant_id);
create index if not exists ix_protocolo_anexo_status on sigov.protocolo_anexo (status);
create index if not exists ix_protocolo_anexo_created_at on sigov.protocolo_anexo (created_at);
create index if not exists ix_documento_tenant_id on sigov.documento (tenant_id);
create index if not exists ix_documento_status on sigov.documento (status);
create index if not exists ix_documento_created_at on sigov.documento (created_at);
create index if not exists ix_documento_versao_tenant_id on sigov.documento_versao (tenant_id);
create index if not exists ix_documento_versao_status on sigov.documento_versao (status);
create index if not exists ix_documento_versao_created_at on sigov.documento_versao (created_at);
create index if not exists ix_ged_pasta_tenant_id on sigov.ged_pasta (tenant_id);
create index if not exists ix_ged_pasta_status on sigov.ged_pasta (status);
create index if not exists ix_ged_pasta_created_at on sigov.ged_pasta (created_at);
create index if not exists ix_workflow_tenant_id on sigov.workflow (tenant_id);
create index if not exists ix_workflow_status on sigov.workflow (status);
create index if not exists ix_workflow_created_at on sigov.workflow (created_at);
create index if not exists ix_workflow_etapa_tenant_id on sigov.workflow_etapa (tenant_id);
create index if not exists ix_workflow_etapa_status on sigov.workflow_etapa (status);
create index if not exists ix_workflow_etapa_created_at on sigov.workflow_etapa (created_at);
create index if not exists ix_workflow_transicao_tenant_id on sigov.workflow_transicao (tenant_id);
create index if not exists ix_workflow_transicao_status on sigov.workflow_transicao (status);
create index if not exists ix_workflow_transicao_created_at on sigov.workflow_transicao (created_at);
create index if not exists ix_workflow_instancia_tenant_id on sigov.workflow_instancia (tenant_id);
create index if not exists ix_workflow_instancia_status on sigov.workflow_instancia (status);
create index if not exists ix_workflow_instancia_created_at on sigov.workflow_instancia (created_at);
create index if not exists ix_workflow_historico_tenant_id on sigov.workflow_historico (tenant_id);
create index if not exists ix_workflow_historico_status on sigov.workflow_historico (status);
create index if not exists ix_workflow_historico_created_at on sigov.workflow_historico (created_at);
create index if not exists ix_tarefa_tenant_id on sigov.tarefa (tenant_id);
create index if not exists ix_tarefa_status on sigov.tarefa (status);
create index if not exists ix_tarefa_created_at on sigov.tarefa (created_at);
create index if not exists ix_notificacao_tenant_id on sigov.notificacao (tenant_id);
create index if not exists ix_notificacao_status on sigov.notificacao (status);
create index if not exists ix_notificacao_created_at on sigov.notificacao (created_at);
create index if not exists ix_notificacao_usuario_tenant_id on sigov.notificacao_usuario (tenant_id);
create index if not exists ix_notificacao_usuario_status on sigov.notificacao_usuario (status);
create index if not exists ix_notificacao_usuario_created_at on sigov.notificacao_usuario (created_at);
create index if not exists ix_portal_validacao_documento_tenant_id on sigov.portal_validacao_documento (tenant_id);
create index if not exists ix_portal_validacao_documento_status on sigov.portal_validacao_documento (status);
create index if not exists ix_portal_validacao_documento_created_at on sigov.portal_validacao_documento (created_at);
create index if not exists ix_api_key_prefixo on sigov.api_key (prefixo);
create index if not exists ix_api_key_escopo_api_key_id on sigov.api_key_escopo (api_key_id);
create index if not exists ix_api_key_escopo_escopo on sigov.api_key_escopo (escopo);
create index if not exists ix_protocolo_numero on sigov.protocolo (numero);
create index if not exists ix_documento_hash_sha256 on sigov.documento (hash_sha256);
create index if not exists ix_portal_validacao_documento_codigo_publico on sigov.portal_validacao_documento (codigo_publico);
create index if not exists ix_protocolo_movimento_protocolo_id on sigov.protocolo_movimento (protocolo_id);
create index if not exists ix_protocolo_anexo_protocolo_id on sigov.protocolo_anexo (protocolo_id);
create index if not exists ix_protocolo_anexo_documento_id on sigov.protocolo_anexo (documento_id);
create index if not exists ix_documento_versao_documento_id on sigov.documento_versao (documento_id);
create index if not exists ix_tarefa_protocolo_id on sigov.tarefa (protocolo_id);
create index if not exists ix_notificacao_usuario_usuario_id on sigov.notificacao_usuario (usuario_id);
create index if not exists ix_outbox_evento_evento on sigov.outbox_evento (evento);
