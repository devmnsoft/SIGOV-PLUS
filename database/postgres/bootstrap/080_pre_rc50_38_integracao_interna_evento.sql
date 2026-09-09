-- Compatibilidade forward-only para RC50.38.
-- As migrations de Saúde/Assistência/Saneamento/Frotas publicadas consultam a
-- relação em NOT EXISTS antes que a RC50.63 a crie. PostgreSQL resolve a
-- relação no parse da instrução, portanto o predicado information_schema não
-- evita a falha em banco vazio. Este contrato reúne as colunas consumidas
-- pelos dois formatos históricos sem executar as órfãs RC50.30/RC50.37.
create schema if not exists sigov;

create table if not exists sigov.integracao_interna_evento (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    origem_modulo varchar(40) not null,
    destino_modulo varchar(40) not null,
    origem varchar(80) generated always as (origem_modulo) stored,
    destino varchar(80) generated always as (destino_modulo) stored,
    tipo_evento varchar(120) not null,
    status varchar(40) not null default 'PENDENTE',
    referencia_tipo varchar(80),
    referencia_id bigint,
    referencia varchar(160),
    payload jsonb not null default '{}'::jsonb,
    erro text,
    detalhe_erro varchar(1000),
    rota_correcao varchar(300),
    preparatoria boolean not null default false,
    correlation_id uuid not null default gen_random_uuid(),
    created_at timestamptz not null default now(),
    processed_at timestamptz,
    created_by bigint,
    is_deleted boolean not null default false,
    auditoria jsonb not null default '{}'::jsonb
);

create index if not exists ix_integracao_interna_fila
    on sigov.integracao_interna_evento (tenant_id, status, created_at, id)
    where not is_deleted;
