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

\echo 'Aplicando RC50.98: GED Smart Workflow e branding de logo...'
\i /database/postgres/migrations/20260902000000_rc50_98_ged_workflow_branding_logo.sql
INSERT INTO sigov.docker_schema_migrations (name) VALUES ('20260902000000_rc50_98_ged_workflow_branding_logo') ON CONFLICT (name) DO NOTHING;

\echo 'Aplicando correção final das pós-condições do LicitaPro...'
\i /database/postgres/migrations/20260903130000_corr_licitapro_postconditions_schema.sql
INSERT INTO sigov.docker_schema_migrations (name) VALUES ('20260903130000_corr_licitapro_postconditions_schema') ON CONFLICT (name) DO NOTHING;

\echo 'Aplicando convergência completa do schema LicitaPro...'
\i /database/postgres/migrations/20260903173000_corr_licitapro_schema_history.sql
INSERT INTO sigov.docker_schema_migrations (name) VALUES ('20260903173000_corr_licitapro_schema_history') ON CONFLICT (name) DO NOTHING;
