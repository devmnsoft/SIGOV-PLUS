-- Separa, sem conversão ou cópia, os contratos UUID históricos que ocuparam
-- nomes reservados ao núcleo governamental bigint. ALTER TABLE preserva dados,
-- OIDs, PKs, FKs, constraints e dependências; índices recebem nomes exclusivos.
do $$
declare
    item record;
    destination text;
    index_item record;
    new_index_name text;
begin
    for item in
        select c.relname as source_name,
               case
                 when c.relname in ('compras_numeracao','compras_fornecedor','compras_fornecedor_contato',
                    'compras_fornecedor_endereco','compras_fornecedor_documento','compras_requisicao',
                    'compras_requisicao_item','compras_aprovacao','compras_cotacao','compras_cotacao_convite',
                    'compras_cotacao_resposta_item','compras_pedido','compras_recebimento','compras_fatura',
                    'compras_devolucao','compras_fornecedor_avaliacao','compras_historico','compras_idempotencia')
                   and (c.relname <> 'compras_cotacao' or exists (
                       select 1 from pg_attribute a where a.attrelid=c.oid and a.attname='requisicao_id' and not a.attisdropped))
                   and (c.relname <> 'compras_recebimento' or exists (
                       select 1 from pg_attribute a where a.attrelid=c.oid and a.attname='pedido_id' and not a.attisdropped))
                 then 'compras_empresarial_' || substring(c.relname from 9)
                 else 'bloco6_' || c.relname
               end as destination_name
        from pg_class c
        join pg_namespace n on n.oid=c.relnamespace
        where n.nspname='sigov' and c.relkind in ('r','p') and c.relname like 'compras\_%' escape '\'
          and exists (
              select 1 from pg_attribute a
              where a.attrelid=c.oid and a.attname in ('id','tenant_id') and not a.attisdropped
                and a.atttypid='uuid'::regtype)
          and c.relname not like 'compras_empresarial\_%' escape '\'
          and c.relname not like 'compras_licitapro\_%' escape '\'
        order by case when c.relname='compras_fornecedor' then 0 else 1 end, c.relname
    loop
        destination := item.destination_name;
        if to_regclass(format('sigov.%I', destination)) is not null then
            raise exception using
              errcode='55000',
              message=format('Separação segura bloqueada: sigov.%I e sigov.%I coexistem; compare contratos e contagens antes de reconciliar.', item.source_name, destination);
        end if;

        -- Evita colisão dos nomes globais de índices quando o núcleo canônico for criado.
        for index_item in
            select idx.relname
            from pg_index ix join pg_class tab on tab.oid=ix.indrelid
            join pg_class idx on idx.oid=ix.indexrelid
            join pg_namespace ns on ns.oid=tab.relnamespace
            where ns.nspname='sigov' and tab.relname=item.source_name
        loop
            new_index_name := left(case when destination like 'compras_empresarial_%'
                then 'ce_' || index_item.relname else 'b6_' || index_item.relname end, 63);
            if to_regclass(format('sigov.%I',new_index_name)) is not null then
                raise exception 'Separação segura bloqueada: índice destino sigov.% já existe.', new_index_name;
            end if;
            execute format('alter index sigov.%I rename to %I',index_item.relname,new_index_name);
        end loop;

        execute format('alter table sigov.%I rename to %I',item.source_name,destination);
        raise notice 'Contrato UUID preservado por rename: sigov.% -> sigov.%',item.source_name,destination;
    end loop;
end $$;
