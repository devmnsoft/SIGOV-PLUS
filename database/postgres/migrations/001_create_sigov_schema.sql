create schema if not exists sigov;

create table if not exists sigov.schema_migrations (
    id bigserial primary key,
    version varchar(50) not null unique,
    description varchar(250) not null,
    checksum varchar(128) not null,
    applied_at timestamptz not null default now()
);
