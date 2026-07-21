create schema if not exists sigov;

create table if not exists sigov.schema_migrations (
    id bigint generated always as identity primary key,
    version varchar(50) not null unique,
    description varchar(250) not null,
    checksum varchar(128) not null,
    category varchar(40) not null default 'schema',
    source varchar(40) not null default 'manifest',
    success boolean not null default true,
    execution_ms bigint null,
    applied_at timestamptz not null default now()
);


alter table sigov.schema_migrations
    add column if not exists category varchar(40) not null default 'schema',
    add column if not exists source varchar(40) not null default 'manifest',
    add column if not exists success boolean not null default true,
    add column if not exists execution_ms bigint null;
