-- Correção aditiva final dos objetos exigidos pelas pós-condições do LicitaPro.
-- As migrations publicadas e o histórico de execução permanecem imutáveis.

create table if not exists sigov.compras_licitapro_fonte (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint not null,
    nome varchar(160) not null,
    tipo varchar(30) not null,
    endpoint_url text,
    configurada boolean not null default false,
    ativa boolean not null default true,
    ultima_sincronizacao_at timestamptz,
    created_at timestamptz not null default now(),
    created_by bigint not null,
    updated_at timestamptz,
    updated_by bigint,
    constraint ck_clp_fonte_tipo check (tipo in ('PNCP', 'PORTAL_PUBLICO', 'OUTRA_OFICIAL')),
    constraint ck_clp_fonte_config check (not configurada or endpoint_url is not null),
    unique (tenant_id, entidade_id, nome)
);

create table if not exists sigov.compras_licitapro_alerta (
    id bigint generated always as identity primary key,
    tenant_id bigint not null,
    entidade_id bigint not null,
    fornecedor_id bigint references sigov.compras_fornecedor(id),
    documento_id bigint references sigov.compras_licitapro_documento(id),
    agenda_id bigint references sigov.compras_licitapro_agenda(id),
    tipo varchar(40) not null,
    mensagem text not null,
    status varchar(20) not null default 'ABERTO',
    vencimento_at timestamptz,
    created_at timestamptz not null default now(),
    created_by bigint not null,
    constraint ck_clp_alerta_status check (status in ('ABERTO', 'CIENTE', 'RESOLVIDO'))
);

-- Bases parcialmente restauradas podem ter as relações, mas não todas as
-- colunas da EXP03. Não são fabricados identificadores para linhas existentes.
alter table sigov.compras_licitapro_fonte
    add column if not exists configurada boolean not null default false,
    add column if not exists endpoint_url text;

alter table sigov.compras_licitapro_alerta
    add column if not exists tenant_id bigint,
    add column if not exists entidade_id bigint,
    add column if not exists status varchar(20),
    add column if not exists vencimento_at timestamptz;

do $$
declare
    index_oid oid;
    index_kind "char";
    index_is_expected boolean;
    index_backs_constraint boolean;
begin
    if not exists (
        select 1
        from pg_constraint c
        where c.conrelid = to_regclass('sigov.compras_licitapro_fonte')
          and c.conname = 'ck_clp_fonte_endpoint_url'
    ) then
        alter table sigov.compras_licitapro_fonte
            add constraint ck_clp_fonte_endpoint_url
            check (not configurada or endpoint_url ~* '^https?://[^[:space:]]+$') not valid;
    end if;

    select c.oid, c.relkind
      into index_oid, index_kind
      from pg_class c
      join pg_namespace n on n.oid = c.relnamespace
     where n.nspname = 'sigov'
       and c.relname = 'ix_clp_alerta_tenant_status_vencimento';

    if index_oid is not null and index_kind not in ('i', 'I') then
        raise exception
            'Objeto incompatível: sigov.ix_clp_alerta_tenant_status_vencimento existe, mas não é índice (relkind=%)',
            index_kind;
    end if;

    if index_oid is not null then
        select i.indrelid = to_regclass('sigov.compras_licitapro_alerta')
               and i.indisvalid
               and i.indnkeyatts = 4
               and i.indnatts = 4
               and i.indexprs is null
               and i.indpred is null
               and (
                   select array_agg(a.attname order by keys.ordinality)
                   from unnest(i.indkey) with ordinality keys(attnum, ordinality)
                   join pg_attribute a
                     on a.attrelid = i.indrelid and a.attnum = keys.attnum
               ) = array['tenant_id', 'entidade_id', 'status', 'vencimento_at']::name[],
               exists (select 1 from pg_constraint constraint_index where constraint_index.conindid = i.indexrelid)
          into index_is_expected, index_backs_constraint
          from pg_index i
         where i.indexrelid = index_oid;

        if not index_is_expected then
            if index_backs_constraint then
                raise exception
                    'Índice incompatível: sigov.ix_clp_alerta_tenant_status_vencimento pertence a uma constraint e não pode ser recriado automaticamente';
            end if;
            drop index sigov.ix_clp_alerta_tenant_status_vencimento;
        end if;
    end if;

    create index if not exists sigov.ix_clp_alerta_tenant_status_vencimento
        on sigov.compras_licitapro_alerta (tenant_id, entidade_id, status, vencimento_at);
end $$;
