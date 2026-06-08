-- SIGOV - bootstrap consolidado obrigatório para Docker/homologação.
-- Este arquivo é aplicado automaticamente pelo serviço db-migrations antes das
-- migrations versionadas. Ele não apaga dados e prepara o schema de controle.

create schema if not exists sigov;

create table if not exists sigov.docker_schema_migrations (
    id bigserial primary key,
    version text not null unique,
    file_path text not null,
    checksum text not null,
    applied_at timestamptz not null default now()
);
