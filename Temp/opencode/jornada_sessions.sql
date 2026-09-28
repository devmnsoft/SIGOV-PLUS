insert into sigov.identidade_sessao(tenant_id, entidade_id, usuario_id, token_hash, expira_at)
select v.t, v.e, v.u, v.h, now() + interval '8 hours'
from (values
  (1::bigint, 9101::bigint, 101::bigint, '9cc0b1735232442554769b85f30d226fa774742558cc649cb6e91f63e4fafe61'::text),
  (1::bigint, 9101::bigint, 102::bigint, 'cc526c41874e00f82263df22200168fe6bd6e5b123fd0088ae2d9e5be422d401'::text),
  (5::bigint, 1::bigint,    1::bigint,  'b0c782d4b21d6abb919ff90fe90d0aba3f74ccae976d90e16dd99f1de36be764'::text)
) as v(t, e, u, h)
where not exists (select 1 from sigov.identidade_sessao s where s.usuario_id = v.u and s.token_hash = v.h);

select 'sess:' || count(*) from sigov.identidade_sessao;
select 'hist_tbl:' || count(*) from pg_catalog.pg_class c join pg_catalog.pg_namespace n on n.oid = c.relnamespace where n.nspname = 'sigov' and c.relname = 'compras_empresarial_historico';