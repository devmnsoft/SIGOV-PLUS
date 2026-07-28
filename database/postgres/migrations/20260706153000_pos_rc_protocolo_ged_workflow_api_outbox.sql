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

create or replace function pg_temp.create_index_when_columns_exist(
    schema_name text,
    table_name text,
    index_name text,
    column_names text[],
    index_expression text,
    predicate text default null)
returns void
language plpgsql
as $function$
declare
    missing_columns text[];
begin
    if to_regclass(format('%I.%I', schema_name, table_name)) is null then
        raise notice 'Index % ignored: table %.% does not exist', index_name, schema_name, table_name;
        return;
    end if;

    select array_agg(requested.column_name order by requested.ordinality)
      into missing_columns
      from unnest(column_names) with ordinality requested(column_name, ordinality)
     where not exists (
         select 1
           from information_schema.columns existing
          where existing.table_schema = schema_name
            and existing.table_name = table_name
            and existing.column_name = requested.column_name);

    if missing_columns is not null then
        raise notice 'Index % ignored: missing columns % on %.%', index_name, missing_columns, schema_name, table_name;
        return;
    end if;

    execute format('create index if not exists %I on %I.%I %s%s',
        index_name,
        schema_name,
        table_name,
        index_expression,
        case when predicate is null then '' else ' where ' || predicate end);
end;
$function$;

select pg_temp.create_index_when_columns_exist('sigov', 'api_key', 'ix_api_key_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key', 'ix_api_key_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key', 'ix_api_key_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key_escopo', 'ix_api_key_escopo_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key_escopo', 'ix_api_key_escopo_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key_escopo', 'ix_api_key_escopo_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_requisicao_log', 'ix_api_requisicao_log_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_requisicao_log', 'ix_api_requisicao_log_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_requisicao_log', 'ix_api_requisicao_log_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'webhook_configuracao', 'ix_webhook_configuracao_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'webhook_configuracao', 'ix_webhook_configuracao_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'webhook_configuracao', 'ix_webhook_configuracao_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'webhook_entrega', 'ix_webhook_entrega_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'webhook_entrega', 'ix_webhook_entrega_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'webhook_entrega', 'ix_webhook_entrega_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'outbox_evento', 'ix_outbox_evento_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'outbox_evento', 'ix_outbox_evento_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'outbox_evento', 'ix_outbox_evento_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo', 'ix_protocolo_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo', 'ix_protocolo_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo', 'ix_protocolo_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_movimento', 'ix_protocolo_movimento_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_movimento', 'ix_protocolo_movimento_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_movimento', 'ix_protocolo_movimento_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_anexo', 'ix_protocolo_anexo_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_anexo', 'ix_protocolo_anexo_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_anexo', 'ix_protocolo_anexo_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento', 'ix_documento_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento', 'ix_documento_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento', 'ix_documento_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento_versao', 'ix_documento_versao_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento_versao', 'ix_documento_versao_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento_versao', 'ix_documento_versao_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'ged_pasta', 'ix_ged_pasta_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'ged_pasta', 'ix_ged_pasta_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'ged_pasta', 'ix_ged_pasta_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow', 'ix_workflow_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow', 'ix_workflow_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow', 'ix_workflow_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_etapa', 'ix_workflow_etapa_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_etapa', 'ix_workflow_etapa_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_etapa', 'ix_workflow_etapa_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_transicao', 'ix_workflow_transicao_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_transicao', 'ix_workflow_transicao_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_transicao', 'ix_workflow_transicao_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_instancia', 'ix_workflow_instancia_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_instancia', 'ix_workflow_instancia_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_instancia', 'ix_workflow_instancia_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_historico', 'ix_workflow_historico_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_historico', 'ix_workflow_historico_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'workflow_historico', 'ix_workflow_historico_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao', 'ix_notificacao_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao', 'ix_notificacao_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao', 'ix_notificacao_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao_usuario', 'ix_notificacao_usuario_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao_usuario', 'ix_notificacao_usuario_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao_usuario', 'ix_notificacao_usuario_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'portal_validacao_documento', 'ix_portal_validacao_documento_tenant_id', array['tenant_id'], '(tenant_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'portal_validacao_documento', 'ix_portal_validacao_documento_status', array['status'], '(status)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'portal_validacao_documento', 'ix_portal_validacao_documento_created_at', array['created_at'], '(created_at)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key', 'ix_api_key_prefixo', array['prefixo'], '(prefixo)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key_escopo', 'ix_api_key_escopo_api_key_id', array['api_key_id'], '(api_key_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'api_key_escopo', 'ix_api_key_escopo_escopo', array['escopo'], '(escopo)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo', 'ix_protocolo_numero', array['numero'], '(numero)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento', 'ix_documento_hash_sha256', array['hash_sha256'], '(hash_sha256)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'portal_validacao_documento', 'ix_portal_validacao_documento_codigo_publico', array['codigo_publico'], '(codigo_publico)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_movimento', 'ix_protocolo_movimento_protocolo_id', array['protocolo_id'], '(protocolo_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_anexo', 'ix_protocolo_anexo_protocolo_id', array['protocolo_id'], '(protocolo_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'protocolo_anexo', 'ix_protocolo_anexo_documento_id', array['documento_id'], '(documento_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'documento_versao', 'ix_documento_versao_documento_id', array['documento_id'], '(documento_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_protocolo_id', array['protocolo_id'], '(protocolo_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'notificacao_usuario', 'ix_notificacao_usuario_usuario_id', array['usuario_id'], '(usuario_id)', null);
select pg_temp.create_index_when_columns_exist('sigov', 'outbox_evento', 'ix_outbox_evento_evento', array['evento'], '(evento)', null);
