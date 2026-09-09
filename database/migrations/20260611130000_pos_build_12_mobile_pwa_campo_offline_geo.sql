-- Migration Pós-Build 12 - Mobile/PWA/Campo/Offline/Geo
-- Arquivo criado para corrigir referência ausente no apply_all_required_migrations.sql
-- A migration completa deve ser implementada na evolução correspondente.

CREATE SCHEMA IF NOT EXISTS sigov;

CREATE TABLE IF NOT EXISTS sigov.mobile_dispositivo (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL,
    usuario_id BIGINT NOT NULL,
    identificador VARCHAR(200) NOT NULL,
    nome VARCHAR(200) NULL,
    plataforma VARCHAR(40) NULL,
    versao_app VARCHAR(40) NULL,
    ativo BOOLEAN NOT NULL DEFAULT TRUE,
    ultimo_sync_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NULL,
    UNIQUE (tenant_id, usuario_id, identificador)
);

CREATE TABLE IF NOT EXISTS sigov.mobile_sync_lote (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL,
    usuario_id BIGINT NOT NULL,
    dispositivo_id BIGINT NULL,
    direcao VARCHAR(40) NOT NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'PENDENTE',
    total_itens INT NOT NULL DEFAULT 0,
    itens_processados INT NOT NULL DEFAULT 0,
    erro TEXT NULL,
    correlation_id UUID NOT NULL DEFAULT gen_random_uuid(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    concluido_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS sigov.mobile_sync_item (
    id BIGSERIAL PRIMARY KEY,
    lote_id BIGINT NOT NULL REFERENCES sigov.mobile_sync_lote(id),
    entidade VARCHAR(120) NOT NULL,
    entidade_id_local VARCHAR(120) NULL,
    entidade_id_servidor BIGINT NULL,
    operacao VARCHAR(40) NOT NULL,
    payload JSONB NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'PENDENTE',
    erro TEXT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    processado_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS sigov.campo_atividade (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL,
    modulo_codigo VARCHAR(80) NOT NULL,
    origem VARCHAR(80) NULL,
    origem_id BIGINT NULL,
    titulo VARCHAR(200) NOT NULL,
    descricao TEXT NULL,
    tipo VARCHAR(80) NOT NULL,
    status VARCHAR(40) NOT NULL DEFAULT 'PENDENTE',
    responsavel_id BIGINT NULL,
    data_agendada TIMESTAMPTZ NULL,
    inicio_at TIMESTAMPTZ NULL,
    fim_at TIMESTAMPTZ NULL,
    prioridade VARCHAR(40) NOT NULL DEFAULT 'MEDIA',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS sigov.campo_evidencia (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL,
    atividade_id BIGINT NULL,
    origem VARCHAR(80) NULL,
    origem_id BIGINT NULL,
    tipo VARCHAR(40) NOT NULL,
    titulo VARCHAR(200) NULL,
    arquivo_path TEXT NULL,
    content_type VARCHAR(120) NULL,
    tamanho_bytes BIGINT NULL,
    latitude NUMERIC(10,7) NULL,
    longitude NUMERIC(10,7) NULL,
    capturado_por BIGINT NULL,
    capturado_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sigov.campo_assinatura (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL,
    atividade_id BIGINT NULL,
    nome_assinante VARCHAR(200) NOT NULL,
    documento_assinante VARCHAR(30) NULL,
    assinatura_hash TEXT NULL,
    latitude NUMERIC(10,7) NULL,
    longitude NUMERIC(10,7) NULL,
    ip VARCHAR(80) NULL,
    user_agent TEXT NULL,
    assinado_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS sigov.campo_localizacao (
    id BIGSERIAL PRIMARY KEY,
    tenant_id BIGINT NOT NULL,
    usuario_id BIGINT NULL,
    dispositivo_id BIGINT NULL,
    atividade_id BIGINT NULL,
    latitude NUMERIC(10,7) NOT NULL,
    longitude NUMERIC(10,7) NOT NULL,
    precisao_metros NUMERIC(14,4) NULL,
    origem VARCHAR(40) NOT NULL DEFAULT 'GPS',
    capturado_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS ix_mobile_dispositivo_tenant ON sigov.mobile_dispositivo(tenant_id);
CREATE INDEX IF NOT EXISTS ix_mobile_sync_lote_tenant ON sigov.mobile_sync_lote(tenant_id);
CREATE INDEX IF NOT EXISTS ix_campo_atividade_tenant ON sigov.campo_atividade(tenant_id);
CREATE INDEX IF NOT EXISTS ix_campo_evidencia_tenant ON sigov.campo_evidencia(tenant_id);
CREATE INDEX IF NOT EXISTS ix_campo_localizacao_tenant ON sigov.campo_localizacao(tenant_id);