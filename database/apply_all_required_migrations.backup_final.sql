\set ON_ERROR_STOP off

\echo 'Aplicando baseline consolidado SIGOV PLUS em modo local tolerante...'

CREATE SCHEMA IF NOT EXISTS sigov;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

\i /database/script_completo.sql

\echo 'Aplicando compatibilizacoes finais...'

CREATE TABLE IF NOT EXISTS sigov.docker_schema_migrations (
    id BIGSERIAL PRIMARY KEY,
    version TEXT NULL,
    name TEXT NULL,
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

ALTER TABLE IF EXISTS sigov.docker_schema_migrations
    ADD COLUMN IF NOT EXISTS version TEXT NULL;

ALTER TABLE IF EXISTS sigov.docker_schema_migrations
    ADD COLUMN IF NOT EXISTS name TEXT NULL;

INSERT INTO sigov.docker_schema_migrations (version, name)
VALUES ('00000000000000', '00000000000000_script_completo_baseline_local_tolerante');

\echo 'Baseline local SIGOV PLUS finalizado.'