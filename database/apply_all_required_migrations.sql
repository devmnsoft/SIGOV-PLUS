\echo 'Aplicando baseline consolidado SIGOV PLUS...'

CREATE SCHEMA IF NOT EXISTS sigov;

\i /database/script_completo.sql

CREATE TABLE IF NOT EXISTS sigov.docker_schema_migrations (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO sigov.docker_schema_migrations (name)
VALUES ('00000000000000_script_completo_baseline')
ON CONFLICT (name) DO NOTHING;

\echo 'Baseline consolidado SIGOV PLUS aplicado com sucesso.'
