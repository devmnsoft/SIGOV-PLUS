select tablename from pg_tables where schemaname='sigov' and tablename like 'compras_empresarial%' order by 1;
select conname, pg_get_constraintdef(oid) from pg_constraint where conrelid='sigov.compras_empresarial_cotacao_resposta_item'::regclass;
select column_name, data_type, is_nullable from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_cotacao_resposta_item' order by ordinal_position;
select indexname, indexdef from pg_indexes where schemaname='sigov' and tablename='compras_empresarial_cotacao_resposta_item';
