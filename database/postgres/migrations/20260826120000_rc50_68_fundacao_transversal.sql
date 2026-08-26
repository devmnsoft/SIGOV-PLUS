-- RC50.68 - fundação transversal canônica, aditiva e idempotente.
-- Não habilita FUNC21/FUNC22/FUNC23/FUNC24 e não contém dados operacionais.

create table if not exists sigov.evidencia_transversal (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint not null,
    tipo varchar(60) not null,
    origem varchar(100) not null,
    entidade_relacionada_tipo varchar(100) not null,
    entidade_relacionada_id bigint not null,
    descricao text not null,
    registrada_at timestamptz not null,
    latitude numeric(10,7),
    longitude numeric(10,7),
    hash_sha256 varchar(64),
    ged_documento_id bigint,
    usuario_responsavel_id bigint not null,
    classificacao_lgpd varchar(30) not null default 'INTERNO',
    created_at timestamptz not null default now(),
    created_by bigint not null,
    updated_at timestamptz,
    updated_by bigint,
    is_deleted boolean not null default false,
    deleted_at timestamptz,
    deleted_by bigint,
    correlation_id uuid,
    constraint ck_evidencia_transversal_tipo check (btrim(tipo) <> ''),
    constraint ck_evidencia_transversal_origem check (btrim(origem) <> ''),
    constraint ck_evidencia_transversal_relacao check (btrim(entidade_relacionada_tipo) <> '' and entidade_relacionada_id > 0),
    constraint ck_evidencia_transversal_descricao check (btrim(descricao) <> ''),
    constraint ck_evidencia_transversal_latitude check (latitude is null or latitude between -90 and 90),
    constraint ck_evidencia_transversal_longitude check (longitude is null or longitude between -180 and 180),
    constraint ck_evidencia_transversal_hash check (hash_sha256 is null or hash_sha256 ~ '^[0-9a-fA-F]{64}$'),
    constraint ck_evidencia_transversal_ged check (ged_documento_id is null or ged_documento_id > 0)
);

create index if not exists ix_evidencia_transversal_relacionada
    on sigov.evidencia_transversal(tenant_id, entidade_id, entidade_relacionada_tipo, entidade_relacionada_id)
    where not is_deleted;
create index if not exists ix_evidencia_transversal_registrada
    on sigov.evidencia_transversal(tenant_id, entidade_id, registrada_at desc)
    where not is_deleted;

create table if not exists sigov.sincronizacao_outbox (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint not null,
    chave_idempotente varchar(160) not null,
    origem varchar(100) not null,
    payload jsonb not null,
    status varchar(30) not null default 'PENDENTE',
    tentativas integer not null default 0,
    erro_sanitizado text,
    criado_at timestamptz not null default now(),
    processamento_at timestamptz,
    concluido_at timestamptz,
    proxima_tentativa_at timestamptz,
    created_by bigint not null,
    updated_at timestamptz,
    updated_by bigint,
    correlation_id uuid,
    constraint ux_sincronizacao_outbox_idempotencia unique (tenant_id, entidade_id, origem, chave_idempotente),
    constraint ck_sincronizacao_outbox_chave check (btrim(chave_idempotente) <> ''),
    constraint ck_sincronizacao_outbox_origem check (btrim(origem) <> ''),
    constraint ck_sincronizacao_outbox_status check (status in ('PENDENTE','PROCESSANDO','CONCLUIDO','FALHA')),
    constraint ck_sincronizacao_outbox_tentativas check (tentativas >= 0),
    constraint ck_sincronizacao_outbox_datas check (
        (status <> 'PROCESSANDO' or processamento_at is not null) and
        (status <> 'CONCLUIDO' or concluido_at is not null))
);

create index if not exists ix_sincronizacao_outbox_pendente
    on sigov.sincronizacao_outbox(tenant_id, entidade_id, status, proxima_tentativa_at, criado_at)
    where status in ('PENDENTE','FALHA');

comment on table sigov.evidencia_transversal is
    'Metadados canônicos de evidência; conteúdo binário permanece no GED quando ged_documento_id for informado.';
comment on table sigov.sincronizacao_outbox is
    'Fila persistida e idempotente para sincronização futura; esta migration não cria worker nem integração externa.';
