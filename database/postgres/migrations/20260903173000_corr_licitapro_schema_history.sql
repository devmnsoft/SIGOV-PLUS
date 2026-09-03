-- Correção aditiva para bancos que aplicaram o conteúdo histórico de 20260903130000.
-- O ledger permanece intocado: esta migration materializa os objetos faltantes no schema final.

do $$
begin
    if to_regclass('sigov.compras_licitapro_fonte') is null then
        raise exception 'LICITAPRO_SCHEMA_INCOMPLETE: tabela sigov.compras_licitapro_fonte ausente';
    end if;
    if to_regclass('sigov.compras_licitapro_alerta') is null then
        raise exception 'LICITAPRO_SCHEMA_INCOMPLETE: tabela sigov.compras_licitapro_alerta ausente';
    end if;
    if exists (
        select 1
        from (values
            ('compras_licitapro_fonte', 'configurada'),
            ('compras_licitapro_fonte', 'endpoint_url'),
            ('compras_licitapro_alerta', 'tenant_id'),
            ('compras_licitapro_alerta', 'entidade_id'),
            ('compras_licitapro_alerta', 'status'),
            ('compras_licitapro_alerta', 'vencimento_at')
        ) required(table_name, column_name)
        where not exists (
            select 1 from information_schema.columns c
            where c.table_schema='sigov'
              and c.table_name=required.table_name
              and c.column_name=required.column_name
        )
    ) then
        raise exception 'LICITAPRO_SCHEMA_INCOMPLETE: colunas necessárias para constraint ou índice ausentes';
    end if;
end $$;

do $$
declare
    existing_type "char";
    is_constraint_index boolean;
    compatible boolean;
begin
    select c.relkind,
           exists (select 1 from pg_constraint con where con.conindid=c.oid),
           c.relkind='i' and i.indrelid=to_regclass('sigov.compras_licitapro_alerta')
             and i.indisvalid and i.indnkeyatts=4 and i.indnatts=4
             and i.indexprs is null and i.indpred is null
             and (select array_agg(a.attname order by keys.ordinality)
                  from unnest(i.indkey) with ordinality keys(attnum, ordinality)
                  join pg_attribute a on a.attrelid=i.indrelid and a.attnum=keys.attnum)
                 = array['tenant_id','entidade_id','status','vencimento_at']::name[]
      into existing_type, is_constraint_index, compatible
      from pg_class c
      join pg_namespace n on n.oid=c.relnamespace
      left join pg_index i on i.indexrelid=c.oid
     where n.nspname='sigov' and c.relname='ix_clp_alerta_tenant_status_vencimento';

    if existing_type is not null and not coalesce(compatible, false) then
        if existing_type <> 'i' or is_constraint_index then
            raise exception 'LICITAPRO_INDEX_CONFLICT: sigov.ix_clp_alerta_tenant_status_vencimento existe, relkind=%, constraint_index=%', existing_type, is_constraint_index;
        end if;
        drop index sigov.ix_clp_alerta_tenant_status_vencimento;
        existing_type := null;
    end if;

    if existing_type is null then
        create index ix_clp_alerta_tenant_status_vencimento
            on sigov.compras_licitapro_alerta
            (tenant_id, entidade_id, status, vencimento_at);
    end if;
end $$;

do $$
begin
    if not exists (
        select 1 from pg_constraint c
        where c.conrelid=to_regclass('sigov.compras_licitapro_fonte')
          and c.conname='ck_clp_fonte_endpoint_url'
    ) then
        alter table sigov.compras_licitapro_fonte
            add constraint ck_clp_fonte_endpoint_url
            check (not configurada or endpoint_url ~* '^https?://[^[:space:]]+$') not valid;
    elsif not exists (
        select 1 from pg_constraint c
        where c.conrelid=to_regclass('sigov.compras_licitapro_fonte')
          and c.conname='ck_clp_fonte_endpoint_url'
          and c.contype='c'
          and pg_get_constraintdef(c.oid) ilike '%configurada%'
          and pg_get_constraintdef(c.oid) ilike '%endpoint_url%'
    ) then
        raise exception 'LICITAPRO_CONSTRAINT_CONFLICT: ck_clp_fonte_endpoint_url existe na tabela correta, mas possui definição incompatível';
    end if;
end $$;
