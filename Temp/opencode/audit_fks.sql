select con.conname, rel.relname || '.' || a.attname || ' -> ' || crel.relname
from pg_constraint con
join pg_class rel on rel.oid = con.conrelid
join pg_class crel on crel.oid = con.confrelid
join pg_attribute a on a.attrelid = con.conrelid and a.attnum = any(con.conkey)
where con.contype = 'f'
  and rel.relname like '%compras%'
order by rel.relname, a.attname;
