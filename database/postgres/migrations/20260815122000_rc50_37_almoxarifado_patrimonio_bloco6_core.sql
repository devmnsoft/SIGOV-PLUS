-- RC50.37 Bloco 6: estruturas públicas multi-tenant e idempotentes.
CREATE SCHEMA IF NOT EXISTS sigov;
CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE TABLE IF NOT EXISTS sigov.almoxarifado (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_tenant ON sigov.almoxarifado(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_tenant_deleted ON sigov.almoxarifado(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_status ON sigov.almoxarifado(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_created ON sigov.almoxarifado(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_item_tenant ON sigov.almoxarifado_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_item_tenant_deleted ON sigov.almoxarifado_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_item_status ON sigov.almoxarifado_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_item_created ON sigov.almoxarifado_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_estoque (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100), almoxarifado_id uuid, item_id uuid, quantidade numeric(18,4) NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_estoque_tenant ON sigov.almoxarifado_estoque(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_estoque_tenant_deleted ON sigov.almoxarifado_estoque(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_estoque_status ON sigov.almoxarifado_estoque(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_estoque_created ON sigov.almoxarifado_estoque(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_movimento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100), tipo varchar(30), origem_id uuid, destino_id uuid, quantidade numeric(18,4) NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_tenant ON sigov.almoxarifado_movimento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_tenant_deleted ON sigov.almoxarifado_movimento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_status ON sigov.almoxarifado_movimento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_created ON sigov.almoxarifado_movimento(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_movimento_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_item_tenant ON sigov.almoxarifado_movimento_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_item_tenant_deleted ON sigov.almoxarifado_movimento_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_item_status ON sigov.almoxarifado_movimento_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_movimento_item_created ON sigov.almoxarifado_movimento_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_requisicao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_tenant ON sigov.almoxarifado_requisicao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_tenant_deleted ON sigov.almoxarifado_requisicao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_status ON sigov.almoxarifado_requisicao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_created ON sigov.almoxarifado_requisicao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_requisicao_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_item_tenant ON sigov.almoxarifado_requisicao_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_item_tenant_deleted ON sigov.almoxarifado_requisicao_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_item_status ON sigov.almoxarifado_requisicao_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_requisicao_item_created ON sigov.almoxarifado_requisicao_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_inventario (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_tenant ON sigov.almoxarifado_inventario(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_tenant_deleted ON sigov.almoxarifado_inventario(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_status ON sigov.almoxarifado_inventario(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_created ON sigov.almoxarifado_inventario(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.almoxarifado_inventario_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_item_tenant ON sigov.almoxarifado_inventario_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_item_tenant_deleted ON sigov.almoxarifado_inventario_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_item_status ON sigov.almoxarifado_inventario_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_almoxarifado_inventario_item_created ON sigov.almoxarifado_inventario_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_bem (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100), tombamento varchar(80), localizacao_id uuid, responsavel_id uuid, contrato_id uuid
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_tenant ON sigov.patrimonio_bem(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_tenant_deleted ON sigov.patrimonio_bem(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_status ON sigov.patrimonio_bem(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_created ON sigov.patrimonio_bem(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_bem_movimento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_movimento_tenant ON sigov.patrimonio_bem_movimento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_movimento_tenant_deleted ON sigov.patrimonio_bem_movimento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_movimento_status ON sigov.patrimonio_bem_movimento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_bem_movimento_created ON sigov.patrimonio_bem_movimento(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_localizacao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_localizacao_tenant ON sigov.patrimonio_localizacao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_localizacao_tenant_deleted ON sigov.patrimonio_localizacao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_localizacao_status ON sigov.patrimonio_localizacao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_localizacao_created ON sigov.patrimonio_localizacao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_responsavel (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_responsavel_tenant ON sigov.patrimonio_responsavel(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_responsavel_tenant_deleted ON sigov.patrimonio_responsavel(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_responsavel_status ON sigov.patrimonio_responsavel(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_responsavel_created ON sigov.patrimonio_responsavel(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_inventario (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_tenant ON sigov.patrimonio_inventario(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_tenant_deleted ON sigov.patrimonio_inventario(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_status ON sigov.patrimonio_inventario(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_created ON sigov.patrimonio_inventario(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_inventario_item (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_item_tenant ON sigov.patrimonio_inventario_item(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_item_tenant_deleted ON sigov.patrimonio_inventario_item(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_item_status ON sigov.patrimonio_inventario_item(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_inventario_item_created ON sigov.patrimonio_inventario_item(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_baixa (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_baixa_tenant ON sigov.patrimonio_baixa(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_baixa_tenant_deleted ON sigov.patrimonio_baixa(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_baixa_status ON sigov.patrimonio_baixa(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_baixa_created ON sigov.patrimonio_baixa(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_manutencao (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_manutencao_tenant ON sigov.patrimonio_manutencao(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_manutencao_tenant_deleted ON sigov.patrimonio_manutencao(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_manutencao_status ON sigov.patrimonio_manutencao(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_manutencao_created ON sigov.patrimonio_manutencao(tenant_id,created_at DESC);
CREATE TABLE IF NOT EXISTS sigov.patrimonio_evento (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(), tenant_id uuid NOT NULL, entidade_id uuid, exercicio_id uuid,
 fornecedor_id uuid, processo_id uuid, solicitacao_id uuid, codigo varchar(80), numero varchar(80), ano integer,
 objeto text, descricao text, tipo varchar(40), status varchar(40) NOT NULL DEFAULT 'RASCUNHO',
 valor_estimado numeric(18,2), valor_homologado numeric(18,2), valor_original numeric(18,2), valor_atual numeric(18,2),
 valor_unitario numeric(18,4), valor_total numeric(18,2), dados jsonb NOT NULL DEFAULT '{}'::jsonb,
 auditoria jsonb NOT NULL DEFAULT '{}'::jsonb, correlation_id varchar(100) NOT NULL, ativo boolean NOT NULL DEFAULT true,
 is_deleted boolean NOT NULL DEFAULT false, created_at timestamptz NOT NULL DEFAULT now(), updated_at timestamptz NOT NULL DEFAULT now(),
 deleted_at timestamptz, created_by varchar(100) NOT NULL, updated_by varchar(100) NOT NULL, deleted_by varchar(100)
);
CREATE INDEX IF NOT EXISTS ix_patrimonio_evento_tenant ON sigov.patrimonio_evento(tenant_id);
CREATE INDEX IF NOT EXISTS ix_patrimonio_evento_tenant_deleted ON sigov.patrimonio_evento(tenant_id,is_deleted);
CREATE INDEX IF NOT EXISTS ix_patrimonio_evento_status ON sigov.patrimonio_evento(tenant_id,status);
CREATE INDEX IF NOT EXISTS ix_patrimonio_evento_created ON sigov.patrimonio_evento(tenant_id,created_at DESC);
CREATE UNIQUE INDEX IF NOT EXISTS ux_patrimonio_tombamento ON sigov.patrimonio_bem(tenant_id,tombamento) WHERE NOT is_deleted;
ALTER TABLE sigov.almoxarifado_estoque ADD CONSTRAINT ck_estoque_nao_negativo CHECK(quantidade >= 0);
CREATE TABLE IF NOT EXISTS sigov.integracao_interna_evento (id uuid PRIMARY KEY DEFAULT gen_random_uuid(),tenant_id uuid NOT NULL,origem varchar(40) NOT NULL,destino varchar(40) NOT NULL,tipo varchar(100) NOT NULL,aggregate_id uuid NOT NULL,payload jsonb NOT NULL DEFAULT '{}'::jsonb,correlation_id varchar(100) NOT NULL,status varchar(20) NOT NULL DEFAULT 'PENDENTE',created_at timestamptz NOT NULL DEFAULT now(),processed_at timestamptz);
CREATE UNIQUE INDEX IF NOT EXISTS ux_integracao_interna_evento ON sigov.integracao_interna_evento(tenant_id,tipo,aggregate_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_almoxarifado_estoque_item ON sigov.almoxarifado_estoque(tenant_id,almoxarifado_id,item_id) WHERE NOT is_deleted;
