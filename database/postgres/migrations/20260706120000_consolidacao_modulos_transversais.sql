-- SIGOV PLUS - consolidação funcional e integração real dos módulos existentes
-- Migration aditiva/idempotente. Não remove, renomeia ou altera tipos existentes.
CREATE SCHEMA IF NOT EXISTS sigov;

CREATE TABLE IF NOT EXISTS sigov.workflow (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, nome TEXT NOT NULL, modulo TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'ativo', created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS sigov.workflow_etapa (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, workflow_id BIGINT NOT NULL, nome TEXT NOT NULL, ordem INT NOT NULL DEFAULT 1, setor_responsavel TEXT NULL, status TEXT NOT NULL DEFAULT 'ativa', created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS sigov.workflow_instancia (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, workflow_id BIGINT NULL, entidade_tipo TEXT NOT NULL, entidade_id TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'em_andamento', etapa_atual_id BIGINT NULL, correlation_id UUID NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS sigov.workflow_historico (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, workflow_instancia_id BIGINT NOT NULL, acao TEXT NOT NULL, observacao TEXT NULL, usuario_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);
CREATE TABLE IF NOT EXISTS sigov.tarefa (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, titulo TEXT NOT NULL, descricao TEXT NULL, entidade_tipo TEXT NULL, entidade_id TEXT NULL, responsavel_id BIGINT NULL, setor_responsavel TEXT NULL, status TEXT NOT NULL DEFAULT 'pendente', prioridade TEXT NOT NULL DEFAULT 'normal', prazo_at TIMESTAMPTZ NULL, correlation_id UUID NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS sigov.notificacao (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, titulo TEXT NOT NULL, mensagem TEXT NOT NULL, tipo TEXT NOT NULL DEFAULT 'info', entidade_tipo TEXT NULL, entidade_id TEXT NULL, status TEXT NOT NULL DEFAULT 'criada', correlation_id UUID NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS sigov.notificacao_usuario (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, notificacao_id BIGINT NOT NULL, usuario_id BIGINT NOT NULL, lida_at TIMESTAMPTZ NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), UNIQUE (tenant_id, notificacao_id, usuario_id)
);
CREATE TABLE IF NOT EXISTS sigov.agenda_prazo (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, titulo TEXT NOT NULL, entidade_tipo TEXT NULL, entidade_id TEXT NULL, prazo_at TIMESTAMPTZ NOT NULL, status TEXT NOT NULL DEFAULT 'aberto', created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false
);
CREATE TABLE IF NOT EXISTS sigov.evento_operacional (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NULL, tipo_evento TEXT NOT NULL, modulo TEXT NOT NULL, entidade_tipo TEXT NULL, entidade_id TEXT NULL, payload JSONB NULL, status TEXT NOT NULL DEFAULT 'registrado', correlation_id UUID NULL, erro TEXT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), processed_at TIMESTAMPTZ NULL
);
CREATE TABLE IF NOT EXISTS sigov.outbox_evento (
  id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NULL, tipo_evento TEXT NOT NULL, aggregate_type TEXT NULL, aggregate_id TEXT NULL, payload JSONB NOT NULL DEFAULT '{}'::jsonb, status TEXT NOT NULL DEFAULT 'pendente', correlation_id UUID NULL, tentativas INT NOT NULL DEFAULT 0, erro TEXT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), processed_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS sigov.protocolo (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, numero TEXT NOT NULL, assunto TEXT NOT NULL, interessado_nome TEXT NULL, interessado_documento TEXT NULL, status TEXT NOT NULL DEFAULT 'aberto', workflow_instancia_id BIGINT NULL, correlation_id UUID NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false, UNIQUE(tenant_id, numero));
CREATE TABLE IF NOT EXISTS sigov.protocolo_movimento (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, protocolo_id BIGINT NOT NULL, origem TEXT NULL, destino TEXT NULL, acao TEXT NOT NULL, observacao TEXT NULL, usuario_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS sigov.protocolo_anexo (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, protocolo_id BIGINT NOT NULL, documento_id BIGINT NULL, arquivo_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.ged_pasta (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, nome TEXT NOT NULL, pasta_pai_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.arquivo (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, nome_original TEXT NOT NULL, content_type TEXT NULL, tamanho_bytes BIGINT NULL, storage_key TEXT NOT NULL, hash_sha256 TEXT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.documento (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, titulo TEXT NOT NULL, tipo TEXT NULL, status TEXT NOT NULL DEFAULT 'rascunho', ged_pasta_id BIGINT NULL, arquivo_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.contrato (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, numero TEXT NOT NULL, objeto TEXT NOT NULL, fornecedor_id BIGINT NULL, status TEXT NOT NULL DEFAULT 'rascunho', vigencia_inicio DATE NULL, vigencia_fim DATE NULL, valor NUMERIC(18,2) NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false, UNIQUE(tenant_id, numero));
CREATE TABLE IF NOT EXISTS sigov.contrato_fiscal (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, contrato_id BIGINT NOT NULL, pessoa_id BIGINT NULL, nome TEXT NULL, tipo TEXT NOT NULL DEFAULT 'fiscal', created_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.contrato_documento (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, contrato_id BIGINT NOT NULL, documento_id BIGINT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.compra_solicitacao (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, numero TEXT NULL, objeto TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'solicitada', valor_estimado NUMERIC(18,2) NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.licitacao (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, numero TEXT NULL, modalidade TEXT NULL, objeto TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'aguardando_integracao', compra_solicitacao_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_bem (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, descricao TEXT NOT NULL, numero_tombamento TEXT NULL, status TEXT NOT NULL DEFAULT 'em_cadastro', localizacao TEXT NULL, contrato_id BIGINT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_movimento (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, patrimonio_bem_id BIGINT NOT NULL, tipo TEXT NOT NULL, origem TEXT NULL, destino TEXT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now());
CREATE TABLE IF NOT EXISTS sigov.patrimonio_inventario (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, descricao TEXT NOT NULL, status TEXT NOT NULL DEFAULT 'aberto', created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.obra (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, nome TEXT NOT NULL, contrato_id BIGINT NULL, fiscal_id BIGINT NULL, status TEXT NOT NULL DEFAULT 'planejada', endereco TEXT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.obra_medicao (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, obra_id BIGINT NOT NULL, competencia DATE NOT NULL, valor NUMERIC(18,2) NULL, status TEXT NOT NULL DEFAULT 'rascunho', created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.obra_diario (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, obra_id BIGINT NOT NULL, data DATE NOT NULL, relato TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), updated_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);
CREATE TABLE IF NOT EXISTS sigov.obra_foto (id BIGSERIAL PRIMARY KEY, tenant_id BIGINT NOT NULL, obra_id BIGINT NOT NULL, arquivo_id BIGINT NOT NULL, legenda TEXT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT now(), is_deleted BOOLEAN NOT NULL DEFAULT false);

CREATE INDEX IF NOT EXISTS ix_workflow_tenant_status ON sigov.workflow(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_workflow_instancia_entidade ON sigov.workflow_instancia(tenant_id,entidade_tipo,entidade_id);
CREATE OR REPLACE FUNCTION pg_temp.create_index_when_columns_exist(
  target_schema text,
  target_table text,
  target_index text,
  target_columns text[],
  target_predicate text DEFAULT NULL
) RETURNS void LANGUAGE plpgsql AS $$
DECLARE
  quoted_columns text;
BEGIN
  IF to_regclass(format('%I.%I', target_schema, target_table)) IS NULL OR target_columns IS NULL OR cardinality(target_columns) = 0 THEN
    RETURN;
  END IF;

  IF EXISTS (
    SELECT 1 FROM unnest(target_columns) AS requested(column_name)
    WHERE NOT EXISTS (
      SELECT 1 FROM information_schema.columns actual
      WHERE actual.table_schema = target_schema
        AND actual.table_name = target_table
        AND actual.column_name = requested.column_name
        AND actual.data_type NOT IN ('ARRAY', 'USER-DEFINED')
    )
  ) THEN
    RETURN;
  END IF;

  SELECT string_agg(format('%I', column_name), ',') INTO quoted_columns FROM unnest(target_columns) AS column_name;
  EXECUTE format('CREATE INDEX IF NOT EXISTS %I ON %I.%I (%s)%s', target_index, target_schema, target_table,
    quoted_columns, CASE WHEN nullif(btrim(target_predicate), '') IS NULL THEN '' ELSE ' WHERE ' || target_predicate END);
END $$;

SELECT pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_tenant_status_prazo', ARRAY['tenant_id','status','prazo_em'], NULL);
SELECT pg_temp.create_index_when_columns_exist('sigov', 'tarefa', 'ix_tarefa_tenant_status_prazo', ARRAY['tenant_id','status','prazo_at'], NULL)
 WHERE NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'sigov' AND indexname = 'ix_tarefa_tenant_status_prazo');
SELECT pg_temp.create_index_when_columns_exist('sigov', 'notificacao', 'ix_notificacao_tenant_status', ARRAY['tenant_id','status','created_at'], NULL);
SELECT pg_temp.create_index_when_columns_exist('sigov', 'notificacao_usuario', 'ix_notificacao_usuario_tenant_usuario', ARRAY['tenant_id','usuario_id','created_at'], NULL);
SELECT pg_temp.create_index_when_columns_exist('sigov', 'outbox_evento', 'ix_outbox_evento_status', ARRAY['status','created_at'], NULL);
CREATE INDEX IF NOT EXISTS ix_evento_operacional_correlation ON sigov.evento_operacional(correlation_id);
SELECT pg_temp.create_index_when_columns_exist('sigov', 'protocolo', 'ix_protocolo_tenant_status', ARRAY['tenant_id','status','created_at'], NULL);
SELECT pg_temp.create_index_when_columns_exist('sigov', 'documento', 'ix_documento_tenant_status', ARRAY['tenant_id','status','created_at'], NULL);
SELECT pg_temp.create_index_when_columns_exist('sigov', 'contrato', 'ix_contrato_tenant_status_vigencia', ARRAY['tenant_id','status','vigencia_fim'], NULL);
CREATE INDEX IF NOT EXISTS ix_compra_solicitacao_tenant_status ON sigov.compra_solicitacao(tenant_id,status,created_at);
CREATE INDEX IF NOT EXISTS ix_licitacao_tenant_status ON sigov.licitacao(tenant_id,status,created_at);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_tenant_status ON sigov.patrimonio_bem(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_obra_tenant_status ON sigov.obra(tenant_id,status);

COMMENT ON TABLE sigov.outbox_evento IS 'Outbox transacional para eventos operacionais SIGOV PLUS com reprocessamento por worker.';
COMMENT ON TABLE sigov.protocolo IS 'Protocolo operacional multi-tenant; não representa simulação quando persistido por fluxo homologado.';
