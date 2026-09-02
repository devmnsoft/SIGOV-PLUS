\echo 'Aplicando baseline consolidado SIGOV PLUS...'

CREATE SCHEMA IF NOT EXISTS sigov;

\i /database/script_completo.sql
\i /database/postgres/migrations/20260902123000_rc50_98_branding_logo_ged_smart_workflow.sql

CREATE TABLE IF NOT EXISTS sigov.docker_schema_migrations (
    id BIGSERIAL PRIMARY KEY,
    name TEXT NOT NULL UNIQUE,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO sigov.docker_schema_migrations (name)
VALUES ('00000000000000_script_completo_baseline')
ON CONFLICT (name) DO NOTHING;

INSERT INTO sigov.docker_schema_migrations (name)
VALUES ('20260902123000_rc50_98_branding_logo_ged_smart_workflow')
ON CONFLICT (name) DO NOTHING;

\echo 'Baseline consolidado SIGOV PLUS aplicado com sucesso.'
