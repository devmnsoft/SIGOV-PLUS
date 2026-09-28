\pset footer off
select 'PEDIDO' as s, column_name, data_type, is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_pedido' order by ordinal_position;
select 'PEDIDO_C' as s, conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='sigov.compras_empresarial_pedido'::regclass order by conname;
select 'PED_ITEM' as s, column_name, data_type, is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_pedido_item' order by ordinal_position;
select 'PED_ITEM_C' as s, conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='sigov.compras_empresarial_pedido_item'::regclass order by conname;
select 'PRODUTO' as s, column_name, data_type, is_nullable from information_schema.columns where table_schema='sigov' and table_name='estoque_produto' order by ordinal_position;
select 'PRODUTO_C' as s, conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='sigov.estoque_produto'::regclass order by conname;
