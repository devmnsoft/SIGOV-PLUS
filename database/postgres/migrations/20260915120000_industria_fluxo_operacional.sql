-- Fluxo industrial: concorrência, idempotência e cadeia de custódia do apontamento.
-- Forward-only e idempotente; preserva os registros industriais publicados.
alter table if exists sigov.industria_ordem_producao
    add column if not exists version bigint not null default 1,
    add column if not exists quantidade_aprovada numeric(14,4) not null default 0,
    add column if not exists quantidade_rejeitada numeric(14,4) not null default 0;

alter table if exists sigov.industria_apontamento
    add column if not exists idempotency_key varchar(160),
    add column if not exists payload_hash varchar(64),
    add column if not exists unidade varchar(20),
    add column if not exists almoxarifado_id bigint,
    add column if not exists lote_produzido varchar(80),
    add column if not exists confirmado_at timestamptz;

alter table if exists sigov.industria_consumo_material
    add column if not exists apontamento_id bigint references sigov.industria_apontamento(id),
    add column if not exists lote varchar(80),
    add column if not exists idempotency_key varchar(160);

alter table if exists sigov.industria_producao_acabada
    add column if not exists apontamento_id bigint references sigov.industria_apontamento(id),
    add column if not exists bloqueado_qualidade boolean not null default false,
    add column if not exists idempotency_key varchar(160);

alter table if exists sigov.industria_inspecao_qualidade
    add column if not exists apontamento_id bigint references sigov.industria_apontamento(id),
    add column if not exists quantidade_inspecionada numeric(14,4),
    add column if not exists quantidade_aprovada numeric(14,4),
    add column if not exists quantidade_rejeitada numeric(14,4);

create unique index if not exists ux_industria_apontamento_idempotencia
    on sigov.industria_apontamento(tenant_id, idempotency_key)
    where idempotency_key is not null;
create unique index if not exists ux_industria_consumo_idempotencia
    on sigov.industria_consumo_material(tenant_id, idempotency_key)
    where idempotency_key is not null;
create unique index if not exists ux_industria_producao_idempotencia
    on sigov.industria_producao_acabada(tenant_id, idempotency_key)
    where idempotency_key is not null;
create index if not exists ix_industria_consumo_apontamento
    on sigov.industria_consumo_material(tenant_id, apontamento_id);
create index if not exists ix_industria_producao_apontamento
    on sigov.industria_producao_acabada(tenant_id, apontamento_id);
create index if not exists ix_industria_inspecao_apontamento
    on sigov.industria_inspecao_qualidade(tenant_id, apontamento_id);

do $$ begin
    if not exists (select 1 from pg_constraint where conname='ck_industria_ordem_quantidades_qualidade') then
        alter table sigov.industria_ordem_producao add constraint ck_industria_ordem_quantidades_qualidade
            check (quantidade_aprovada >= 0 and quantidade_rejeitada >= 0
                   and quantidade_aprovada + quantidade_rejeitada <= quantidade_produzida);
    end if;
    if not exists (select 1 from pg_constraint where conname='ck_industria_inspecao_quantidades') then
        alter table sigov.industria_inspecao_qualidade add constraint ck_industria_inspecao_quantidades
            check (quantidade_inspecionada is null or
                   (quantidade_inspecionada > 0 and coalesce(quantidade_aprovada,0) >= 0
                    and coalesce(quantidade_rejeitada,0) >= 0
                    and coalesce(quantidade_aprovada,0) + coalesce(quantidade_rejeitada,0) <= quantidade_inspecionada));
    end if;
end $$;
