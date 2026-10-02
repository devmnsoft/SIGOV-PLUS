-- RC50.37 Bloco 6: estruturas públicas multi-tenant e idempotentes.
CREATE SCHEMA IF NOT EXISTS sigov;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE TABLE IF NOT EXISTS sigov.compras_solicitacao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_tenant ON sigov.compras_solicitacao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_tenant_deleted ON sigov.compras_solicitacao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_status ON sigov.compras_solicitacao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_created ON sigov.compras_solicitacao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_solicitacao_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_item_tenant ON sigov.compras_solicitacao_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_item_tenant_deleted ON sigov.compras_solicitacao_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_item_status ON sigov.compras_solicitacao_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_solicitacao_item_created ON sigov.compras_solicitacao_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_cotacao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_tenant ON sigov.compras_cotacao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_tenant_deleted ON sigov.compras_cotacao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_status ON sigov.compras_cotacao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_created ON sigov.compras_cotacao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_cotacao_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_item_tenant ON sigov.compras_cotacao_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_item_tenant_deleted ON sigov.compras_cotacao_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_item_status ON sigov.compras_cotacao_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_cotacao_item_created ON sigov.compras_cotacao_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_mapa_comparativo (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_mapa_comparativo_tenant ON sigov.compras_mapa_comparativo(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_mapa_comparativo_tenant_deleted ON sigov.compras_mapa_comparativo(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_mapa_comparativo_status ON sigov.compras_mapa_comparativo(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_mapa_comparativo_created ON sigov.compras_mapa_comparativo(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_processo (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_processo_tenant ON sigov.compras_processo(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_processo_tenant_deleted ON sigov.compras_processo(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_processo_status ON sigov.compras_processo(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_processo_created ON sigov.compras_processo(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_modalidade (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_modalidade_tenant ON sigov.compras_modalidade(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_modalidade_tenant_deleted ON sigov.compras_modalidade(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_modalidade_status ON sigov.compras_modalidade(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_modalidade_created ON sigov.compras_modalidade(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_julgamento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_tenant ON sigov.compras_julgamento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_tenant_deleted ON sigov.compras_julgamento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_status ON sigov.compras_julgamento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_created ON sigov.compras_julgamento(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_julgamento_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_item_tenant ON sigov.compras_julgamento_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_item_tenant_deleted ON sigov.compras_julgamento_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_item_status ON sigov.compras_julgamento_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_julgamento_item_created ON sigov.compras_julgamento_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_autorizacao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_autorizacao_tenant ON sigov.compras_autorizacao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_autorizacao_tenant_deleted ON sigov.compras_autorizacao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_autorizacao_status ON sigov.compras_autorizacao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_autorizacao_created ON sigov.compras_autorizacao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_ordem_compra (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_tenant ON sigov.compras_ordem_compra(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_tenant_deleted ON sigov.compras_ordem_compra(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_status ON sigov.compras_ordem_compra(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_created ON sigov.compras_ordem_compra(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_ordem_compra_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_item_tenant ON sigov.compras_ordem_compra_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_item_tenant_deleted ON sigov.compras_ordem_compra_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_item_status ON sigov.compras_ordem_compra_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_ordem_compra_item_created ON sigov.compras_ordem_compra_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_integracao_financeira (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_integracao_financeira_tenant ON sigov.compras_integracao_financeira(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_integracao_financeira_tenant_deleted ON sigov.compras_integracao_financeira(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_integracao_financeira_status ON sigov.compras_integracao_financeira(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_integracao_financeira_created ON sigov.compras_integracao_financeira(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.compras_evento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_compras_evento_tenant ON sigov.compras_evento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_compras_evento_tenant_deleted ON sigov.compras_evento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_compras_evento_status ON sigov.compras_evento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_compras_evento_created ON sigov.compras_evento(tenant_id,created_at DESC);
ALTER TABLE sigov.compras_solicitacao DROP CONSTRAINT IF EXISTS ck_compras_solicitacao_status;
ALTER TABLE sigov.compras_solicitacao ADD CONSTRAINT ck_compras_solicitacao_status CHECK(status IN ('RASCUNHO','ABERTA','EM_COTACAO','COTADA','AUTORIZADA','REPROVADA','CANCELADA','CONCLUIDA'));
CREATE UNIQUE INDEX IF NOT EXISTS ux_compras_ordem_integracao ON sigov.compras_integracao_financeira(tenant_id, dados) WHERE NOT is_deleted;
