-- Correção aditiva final dos objetos exigidos pelas pós-condições do LicitaPro.
-- As migrations publicadas e o histórico de execução permanecem imutáveis.

do $$
begin
    if to_regclass('sigov.compras_licitapro_fonte') is null then
        raise exception 'Schema LicitaPro incompleto: relação sigov.compras_licitapro_fonte ausente';
    end if;

    if to_regclass('sigov.compras_licitapro_alerta') is null then
        raise exception 'Schema LicitaPro incompleto: relação sigov.compras_licitapro_alerta ausente';
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
            select 1
            from information_schema.columns c
            where c.table_schema = 'sigov'
              and c.table_name = required.table_name
              and c.column_name = required.column_name
        )
    ) then
        raise exception 'Schema LicitaPro incompleto: uma ou mais colunas canônicas estão ausentes';
    end if;
end $$;

do $$
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
end $$;

create index if not exists sigov.ix_clp_alerta_tenant_status_vencimento
    on sigov.compras_licitapro_alerta (tenant_id, entidade_id, status, vencimento_at);
