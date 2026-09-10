\set ON_ERROR_STOP on
\echo 'Aplicando baseline canônico SIGOV PLUS (somente entradas declaradas includeInBaseline=true; automáticas e históricas são governadas pelo manifest)...'

\i /database/script_completo.sql

create table if not exists sigov.docker_schema_migrations (
    id bigint generated always as identity primary key,
    name text not null unique,
    applied_at timestamptz not null default now()
);

insert into sigov.docker_schema_migrations (name)
values ('00000000000000_script_completo_baseline')
on conflict (name) do nothing;

\echo 'Baseline canônico SIGOV PLUS aplicado com sucesso.'
