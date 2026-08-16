-- RC50.37 Bloco 6: estruturas públicas multi-tenant e idempotentes.
CREATE SCHEMA IF NOT EXISTS sigov;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE TABLE IF NOT EXISTS sigov.contrato (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100), data_assinatura date, vigencia_inicio date, vigencia_fim date, saldo numeric(18,2)
);
CREATE INDEX IF NOT EXISTS ix_contrato_tenant ON sigov.contrato(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_tenant_deleted ON sigov.contrato(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_status ON sigov.contrato(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_created ON sigov.contrato(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_item_tenant ON sigov.contrato_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_item_tenant_deleted ON sigov.contrato_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_item_status ON sigov.contrato_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_item_created ON sigov.contrato_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_fornecedor (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_fornecedor_tenant ON sigov.contrato_fornecedor(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_fornecedor_tenant_deleted ON sigov.contrato_fornecedor(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_fornecedor_status ON sigov.contrato_fornecedor(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_fornecedor_created ON sigov.contrato_fornecedor(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_fiscal (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_fiscal_tenant ON sigov.contrato_fiscal(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_fiscal_tenant_deleted ON sigov.contrato_fiscal(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_fiscal_status ON sigov.contrato_fiscal(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_fiscal_created ON sigov.contrato_fiscal(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_vigencia (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_vigencia_tenant ON sigov.contrato_vigencia(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_vigencia_tenant_deleted ON sigov.contrato_vigencia(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_vigencia_status ON sigov.contrato_vigencia(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_vigencia_created ON sigov.contrato_vigencia(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_aditivo (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_aditivo_tenant ON sigov.contrato_aditivo(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_aditivo_tenant_deleted ON sigov.contrato_aditivo(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_aditivo_status ON sigov.contrato_aditivo(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_aditivo_created ON sigov.contrato_aditivo(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_apostilamento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_apostilamento_tenant ON sigov.contrato_apostilamento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_apostilamento_tenant_deleted ON sigov.contrato_apostilamento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_apostilamento_status ON sigov.contrato_apostilamento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_apostilamento_created ON sigov.contrato_apostilamento(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_medicao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_tenant ON sigov.contrato_medicao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_tenant_deleted ON sigov.contrato_medicao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_status ON sigov.contrato_medicao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_created ON sigov.contrato_medicao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_medicao_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_item_tenant ON sigov.contrato_medicao_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_item_tenant_deleted ON sigov.contrato_medicao_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_item_status ON sigov.contrato_medicao_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_medicao_item_created ON sigov.contrato_medicao_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_pagamento_previsto (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_pagamento_previsto_tenant ON sigov.contrato_pagamento_previsto(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_pagamento_previsto_tenant_deleted ON sigov.contrato_pagamento_previsto(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_pagamento_previsto_status ON sigov.contrato_pagamento_previsto(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_pagamento_previsto_created ON sigov.contrato_pagamento_previsto(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_saldo (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_saldo_tenant ON sigov.contrato_saldo(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_saldo_tenant_deleted ON sigov.contrato_saldo(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_saldo_status ON sigov.contrato_saldo(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_saldo_created ON sigov.contrato_saldo(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_alerta (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_alerta_tenant ON sigov.contrato_alerta(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_alerta_tenant_deleted ON sigov.contrato_alerta(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_alerta_status ON sigov.contrato_alerta(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_alerta_created ON sigov.contrato_alerta(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_integracao_financeira (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_integracao_financeira_tenant ON sigov.contrato_integracao_financeira(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_integracao_financeira_tenant_deleted ON sigov.contrato_integracao_financeira(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_integracao_financeira_status ON sigov.contrato_integracao_financeira(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_integracao_financeira_created ON sigov.contrato_integracao_financeira(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.contrato_evento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_contrato_evento_tenant ON sigov.contrato_evento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_contrato_evento_tenant_deleted ON sigov.contrato_evento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_contrato_evento_status ON sigov.contrato_evento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_contrato_evento_created ON sigov.contrato_evento(tenant_id,created_at DESC);
ALTER TABLE sigov.contrato DROP CONSTRAINT IF EXISTS ck_contrato_vigencia;
ALTER TABLE sigov.contrato ADD CONSTRAINT ck_contrato_vigencia CHECK(vigencia_fim > vigencia_inicio);
ALTER TABLE sigov.contrato DROP CONSTRAINT IF EXISTS ck_contrato_valor;
ALTER TABLE sigov.contrato ADD CONSTRAINT ck_contrato_valor CHECK(valor_original > 0);
