-- Diagnóstico reutilizável de contratos físicos (PostgreSQL 16+).
-- O último result set deve retornar zero linhas para concluir a correção.
\pset null '(null)'
select c.relname as tabela, format_type(id.atttypid,id.atttypmod) as tipo_id,
 format_type(tenant.atttypid,tenant.atttypmod) as tipo_tenant_id,
 format_type(entidade.atttypid,entidade.atttypmod) as tipo_entidade_id,
 pg_get_constraintdef(pk.oid,true) as chave_primaria, c.reltuples::bigint as registros_estimados,
 coalesce(array_agg(distinct idx.relname) filter(where ix.indexrelid is not null and not ix.indisvalid),'{}') as indices_invalidos
from pg_class c join pg_namespace n on n.oid=c.relnamespace
left join pg_attribute id on id.attrelid=c.oid and id.attname='id' and not id.attisdropped
left join pg_attribute tenant on tenant.attrelid=c.oid and tenant.attname='tenant_id' and not tenant.attisdropped
left join pg_attribute entidade on entidade.attrelid=c.oid and entidade.attname='entidade_id' and not entidade.attisdropped
left join pg_constraint pk on pk.conrelid=c.oid and pk.contype='p'
left join pg_index ix on ix.indrelid=c.oid and not ix.indisvalid left join pg_class idx on idx.oid=ix.indexrelid
where n.nspname='sigov' and c.relkind in ('r','p') and c.relname like 'compras\_%' escape '\'
group by c.relname,id.atttypid,id.atttypmod,tenant.atttypid,tenant.atttypmod,entidade.atttypid,entidade.atttypmod,pk.oid,c.reltuples order by c.relname;

select child.relname as tabela_filha, child_col.attname as coluna_filha,
 format_type(child_col.atttypid,child_col.atttypmod) as tipo_filho, parent.relname as tabela_referenciada,
 parent_col.attname as coluna_referenciada, format_type(parent_col.atttypid,parent_col.atttypmod) as tipo_referenciado,
 con.conname,con.convalidated,pg_get_constraintdef(con.oid,true) as definicao
from pg_constraint con join pg_class child on child.oid=con.conrelid join pg_namespace ns on ns.oid=child.relnamespace
join pg_class parent on parent.oid=con.confrelid
join lateral unnest(con.conkey,con.confkey) with ordinality keys(child_attnum,parent_attnum,ord) on true
join pg_attribute child_col on child_col.attrelid=child.oid and child_col.attnum=keys.child_attnum
join pg_attribute parent_col on parent_col.attrelid=parent.oid and parent_col.attnum=keys.parent_attnum
where con.contype='f' and ns.nspname='sigov' and (child.relname like 'compras\_%' escape '\' or parent.relname like 'compras\_%' escape '\')
order by child.relname,con.conname,keys.ord;

-- RESULTADO DE ACEITE: zero linhas em todo o banco, não apenas em Compras.
select con.conname,ns.nspname as schema_filho,child.relname as tabela_filha,child_col.attname as coluna_filha,
 format_type(child_col.atttypid,child_col.atttypmod) as tipo_filho,parent_ns.nspname as schema_pai,
 parent.relname as tabela_pai,parent_col.attname as coluna_pai,format_type(parent_col.atttypid,parent_col.atttypmod) as tipo_pai
from pg_constraint con join pg_class child on child.oid=con.conrelid join pg_namespace ns on ns.oid=child.relnamespace
join pg_class parent on parent.oid=con.confrelid join pg_namespace parent_ns on parent_ns.oid=parent.relnamespace
join lateral unnest(con.conkey,con.confkey) with ordinality keys(child_attnum,parent_attnum,ord) on true
join pg_attribute child_col on child_col.attrelid=child.oid and child_col.attnum=keys.child_attnum
join pg_attribute parent_col on parent_col.attrelid=parent.oid and parent_col.attnum=keys.parent_attnum
where child_col.atttypid<>parent_col.atttypid or child_col.atttypmod<>parent_col.atttypmod
order by ns.nspname,child.relname,con.conname,keys.ord;
