select tablename from pg_tables where schemaname='sigov' and tablename like 'compras%' and tablename not like 'compras_empresarial%' order by 1;
select filename from (select 1) x where false;
