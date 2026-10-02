select 'HAS_TENANT '||t.table_name||'='||case when ic.column_name is null then 'N' else 'Y' end
from information_schema.tables t
left join information_schema.columns ic on ic.table_schema=t.table_schema and ic.table_name=t.table_name and ic.column_name='tenant_id'
where t.table_schema='sigov' and t.table_name like 'compras_empresarial%' order by 1;