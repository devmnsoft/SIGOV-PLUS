-- RC50.98 - Branding logo upload metadata and GED smart workflow startup schema
-- Idempotent corrective migration. Do not edit published migrations.

CREATE SCHEMA IF NOT EXISTS ged;

ALTER TABLE IF EXISTS sigov.saas_tenant_branding
    ADD COLUMN IF NOT EXISTS logo_width_px integer NOT NULL DEFAULT 220,
    ADD COLUMN IF NOT EXISTS logo_height_px integer NOT NULL DEFAULT 72,
    ADD COLUMN IF NOT EXISTS logo_fit varchar(20) NOT NULL DEFAULT 'contain',
    ADD COLUMN IF NOT EXISTS logo_original_filename varchar(260),
    ADD COLUMN IF NOT EXISTS logo_content_type varchar(80),
    ADD COLUMN IF NOT EXISTS logo_size_bytes bigint,
    ADD COLUMN IF NOT EXISTS logo_uploaded_at timestamptz;

DO $$
BEGIN
    IF to_regclass('sigov.saas_tenant_branding') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_saas_tenant_branding_logo_width_px') THEN
        ALTER TABLE sigov.saas_tenant_branding
            ADD CONSTRAINT ck_saas_tenant_branding_logo_width_px CHECK (logo_width_px BETWEEN 80 AND 480);
    END IF;

    IF to_regclass('sigov.saas_tenant_branding') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_saas_tenant_branding_logo_height_px') THEN
        ALTER TABLE sigov.saas_tenant_branding
            ADD CONSTRAINT ck_saas_tenant_branding_logo_height_px CHECK (logo_height_px BETWEEN 32 AND 180);
    END IF;

    IF to_regclass('sigov.saas_tenant_branding') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_saas_tenant_branding_logo_fit') THEN
        ALTER TABLE sigov.saas_tenant_branding
            ADD CONSTRAINT ck_saas_tenant_branding_logo_fit CHECK (logo_fit IN ('contain','cover','fill'));
    END IF;

    IF to_regclass('sigov.saas_tenant_branding') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_saas_tenant_branding_logo_size_bytes') THEN
        ALTER TABLE sigov.saas_tenant_branding
            ADD CONSTRAINT ck_saas_tenant_branding_logo_size_bytes CHECK (logo_size_bytes IS NULL OR logo_size_bytes BETWEEN 0 AND 2097152);
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS ged.smart_workflow_rule (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id bigint,
    entidade_id bigint,
    exercicio_id bigint,
    codigo varchar(120) NOT NULL,
    nome varchar(200) NOT NULL,
    descricao text,
    modulo_codigo varchar(80) NOT NULL DEFAULT 'GED',
    tipo_regra varchar(60) NOT NULL DEFAULT 'AUTOMACAO',
    condicao_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    acao_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    prioridade integer NOT NULL DEFAULT 0,
    ativo boolean NOT NULL DEFAULT true,
    status varchar(40) NOT NULL DEFAULT 'ATIVA',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint,
    updated_by bigint,
    CONSTRAINT ck_smart_workflow_rule_prioridade CHECK (prioridade >= 0),
    CONSTRAINT ck_smart_workflow_rule_status CHECK (status IN ('RASCUNHO','ATIVA','INATIVA','SUSPENSA')),
    CONSTRAINT ck_smart_workflow_rule_codigo_not_blank CHECK (length(trim(codigo)) > 0),
    CONSTRAINT ck_smart_workflow_rule_nome_not_blank CHECK (length(trim(nome)) > 0)
);

CREATE TABLE IF NOT EXISTS ged.smart_workflow_task (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id bigint,
    entidade_id bigint,
    exercicio_id bigint,
    rule_id bigint REFERENCES ged.smart_workflow_rule(id),
    documento_id bigint,
    processo_id bigint,
    titulo varchar(220) NOT NULL,
    descricao text,
    status varchar(40) NOT NULL DEFAULT 'PENDENTE',
    prioridade integer NOT NULL DEFAULT 0,
    responsavel_usuario_id bigint,
    due_at timestamptz,
    started_at timestamptz,
    completed_at timestamptz,
    payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    resultado_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    erro_sanitizado text,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint,
    updated_by bigint,
    CONSTRAINT ck_smart_workflow_task_status CHECK (status IN ('PENDENTE','EM_EXECUCAO','CONCLUIDA','FALHA','CANCELADA')),
    CONSTRAINT ck_smart_workflow_task_prioridade CHECK (prioridade BETWEEN 0 AND 100),
    CONSTRAINT ck_smart_workflow_task_titulo_not_blank CHECK (length(trim(titulo)) > 0),
    CONSTRAINT ck_smart_workflow_task_dates CHECK (completed_at IS NULL OR started_at IS NULL OR completed_at >= started_at)
);

CREATE TABLE IF NOT EXISTS ged.smart_workflow_event (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id bigint,
    entidade_id bigint,
    exercicio_id bigint,
    task_id bigint REFERENCES ged.smart_workflow_task(id),
    rule_id bigint REFERENCES ged.smart_workflow_rule(id),
    tipo_evento varchar(80) NOT NULL,
    origem varchar(120) NOT NULL DEFAULT 'GED',
    origem_id bigint,
    severidade varchar(30) NOT NULL DEFAULT 'INFO',
    payload_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    correlation_id uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint,
    CONSTRAINT ck_smart_workflow_event_severidade CHECK (severidade IN ('INFO','AVISO','ERRO','CRITICO')),
    CONSTRAINT ck_smart_workflow_event_tipo_not_blank CHECK (length(trim(tipo_evento)) > 0),
    CONSTRAINT ck_smart_workflow_event_origem_not_blank CHECK (length(trim(origem)) > 0)
);

CREATE TABLE IF NOT EXISTS ged.smart_workflow_dashboard_snapshot (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    tenant_id bigint,
    entidade_id bigint,
    exercicio_id bigint,
    referencia_data date NOT NULL,
    total_tasks integer NOT NULL DEFAULT 0,
    pendentes integer NOT NULL DEFAULT 0,
    em_execucao integer NOT NULL DEFAULT 0,
    concluidas integer NOT NULL DEFAULT 0,
    falhas integer NOT NULL DEFAULT 0,
    atrasadas integer NOT NULL DEFAULT 0,
    indicadores_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    created_by bigint,
    CONSTRAINT ck_smart_workflow_dashboard_totais CHECK (total_tasks >= 0 AND pendentes >= 0 AND em_execucao >= 0 AND concluidas >= 0 AND falhas >= 0 AND atrasadas >= 0),
    CONSTRAINT uq_smart_workflow_dashboard_snapshot_ref UNIQUE (tenant_id, entidade_id, exercicio_id, referencia_data)
);

DO $$
BEGIN
    IF to_regclass('sigov.tenant') IS NOT NULL THEN
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_smart_workflow_rule_tenant') THEN
            ALTER TABLE ged.smart_workflow_rule ADD CONSTRAINT fk_smart_workflow_rule_tenant FOREIGN KEY (tenant_id) REFERENCES sigov.tenant(id);
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_smart_workflow_task_tenant') THEN
            ALTER TABLE ged.smart_workflow_task ADD CONSTRAINT fk_smart_workflow_task_tenant FOREIGN KEY (tenant_id) REFERENCES sigov.tenant(id);
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_smart_workflow_event_tenant') THEN
            ALTER TABLE ged.smart_workflow_event ADD CONSTRAINT fk_smart_workflow_event_tenant FOREIGN KEY (tenant_id) REFERENCES sigov.tenant(id);
        END IF;
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_smart_workflow_dashboard_tenant') THEN
            ALTER TABLE ged.smart_workflow_dashboard_snapshot ADD CONSTRAINT fk_smart_workflow_dashboard_tenant FOREIGN KEY (tenant_id) REFERENCES sigov.tenant(id);
        END IF;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_smart_workflow_rule_tenant_status ON ged.smart_workflow_rule (tenant_id, status, ativo);
CREATE INDEX IF NOT EXISTS ix_smart_workflow_rule_codigo ON ged.smart_workflow_rule (codigo);
CREATE INDEX IF NOT EXISTS ix_smart_workflow_task_tenant_status ON ged.smart_workflow_task (tenant_id, status, prioridade);
CREATE INDEX IF NOT EXISTS ix_smart_workflow_task_responsavel ON ged.smart_workflow_task (tenant_id, responsavel_usuario_id, status);
CREATE INDEX IF NOT EXISTS ix_smart_workflow_task_due_at ON ged.smart_workflow_task (due_at) WHERE due_at IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_smart_workflow_event_tenant_created ON ged.smart_workflow_event (tenant_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_smart_workflow_event_task ON ged.smart_workflow_event (task_id, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_smart_workflow_dashboard_ref ON ged.smart_workflow_dashboard_snapshot (tenant_id, referencia_data DESC);

DO $$
BEGIN
    IF to_regclass('sigov.docker_schema_migrations') IS NOT NULL THEN
        INSERT INTO sigov.docker_schema_migrations (name)
        VALUES ('20260902123000_rc50_98_branding_logo_ged_smart_workflow')
        ON CONFLICT (name) DO NOTHING;
    END IF;
END $$;
