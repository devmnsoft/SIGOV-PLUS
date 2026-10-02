CREATE TABLE IF NOT EXISTS sigov.usuario_preferencia (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id BIGINT NULL REFERENCES sigov.tenant(id),
  usuario_id BIGINT NOT NULL REFERENCES sigov.usuario(id),
  chave VARCHAR(150) NOT NULL,
  valor JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NULL,
  CONSTRAINT uq_usuario_preferencia_tenant_usuario_chave UNIQUE (tenant_id, usuario_id, chave)
);

CREATE TABLE IF NOT EXISTS sigov.usuario_filtro_salvo (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES sigov.tenant(id),
  usuario_id BIGINT NOT NULL REFERENCES sigov.usuario(id),
  modulo VARCHAR(100) NOT NULL,
  recurso VARCHAR(150) NOT NULL,
  nome VARCHAR(150) NOT NULL,
  filtros_json JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NULL
);

CREATE INDEX IF NOT EXISTS ix_usuario_filtro_salvo_tenant_usuario ON sigov.usuario_filtro_salvo (tenant_id, usuario_id, modulo, recurso);

CREATE TABLE IF NOT EXISTS sigov.onboarding_jornada (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES sigov.tenant(id),
  nome VARCHAR(180) NOT NULL,
  status VARCHAR(40) NOT NULL,
  progresso_percentual NUMERIC(5,2) NOT NULL DEFAULT 0,
  iniciado_at TIMESTAMPTZ NULL,
  concluido_at TIMESTAMPTZ NULL,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS sigov.onboarding_etapa (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES sigov.tenant(id),
  jornada_id BIGINT NOT NULL REFERENCES sigov.onboarding_jornada(id),
  codigo VARCHAR(80) NOT NULL,
  nome VARCHAR(180) NOT NULL,
  descricao TEXT NOT NULL,
  ordem INTEGER NOT NULL,
  status VARCHAR(40) NOT NULL,
  progresso_percentual NUMERIC(5,2) NOT NULL DEFAULT 0,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  updated_at TIMESTAMPTZ NULL
);

CREATE TABLE IF NOT EXISTS sigov.onboarding_tarefa (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES sigov.tenant(id),
  jornada_id BIGINT NOT NULL REFERENCES sigov.onboarding_jornada(id),
  codigo VARCHAR(80) NOT NULL,
  titulo VARCHAR(180) NOT NULL,
  descricao TEXT NOT NULL,
  ordem INTEGER NOT NULL,
  obrigatoria BOOLEAN NOT NULL DEFAULT true,
  status VARCHAR(40) NOT NULL,
  rota_destino VARCHAR(250) NULL,
  concluida_at TIMESTAMPTZ NULL,
  concluida_by BIGINT NULL REFERENCES sigov.usuario(id),
  metadados JSONB NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS sigov.onboarding_evento (
  id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  tenant_id BIGINT NOT NULL REFERENCES sigov.tenant(id),
  jornada_id BIGINT NOT NULL REFERENCES sigov.onboarding_jornada(id),
  tipo_evento VARCHAR(80) NOT NULL,
  descricao TEXT NOT NULL,
  metadados JSONB NOT NULL DEFAULT '{}'::jsonb,
  created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
  created_by BIGINT NULL REFERENCES sigov.usuario(id)
);

CREATE INDEX IF NOT EXISTS ix_onboarding_jornada_tenant ON sigov.onboarding_jornada (tenant_id, status);
CREATE INDEX IF NOT EXISTS ix_onboarding_etapa_tenant_jornada ON sigov.onboarding_etapa (tenant_id, jornada_id, ordem);
CREATE INDEX IF NOT EXISTS ix_onboarding_tarefa_tenant_jornada ON sigov.onboarding_tarefa (tenant_id, jornada_id, ordem, status);
CREATE INDEX IF NOT EXISTS ix_onboarding_evento_tenant_jornada ON sigov.onboarding_evento (tenant_id, jornada_id, created_at);
