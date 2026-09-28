\pset footer off
select 'RECB' as s, column_name, is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento' order by ordinal_position;
select 'RECB_C' as s, conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='sigov.compras_empresarial_recebimento'::regclass order by conname;
select 'ITEM' as s, column_name, is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_item' order by ordinal_position;
select 'ITEM_C' as s, conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='sigov.compras_empresarial_recebimento_item'::regclass order by conname;
select 'TBL' as s, table_name from information_schema.tables where table_schema='sigov' and (table_name ilike '%tenant%' or table_name ilike '%empresa%' or table_name='usuario' or table_name='os_tecnico') order by 2;
