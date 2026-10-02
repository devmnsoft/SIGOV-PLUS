select conname, pg_get_constraintdef(c.oid) def
from pg_constraint c join pg_namespace n on n.oid=c.connamespace
where n.nspname='sigov' and c.conrelid::regclass::text in (
 'sigov.compras_empresarial_cotacao','sigov.compras_empresarial_cotacao_convite','sigov.compras_empresarial_cotacao_resposta_item',
 'sigov.compras_empresarial_pedido','sigov.compras_empresarial_pedido_item','sigov.compras_empresarial_idempotencia','sigov.compras_empresarial_aprovacao','sigov.compras_empresarial_recebimento','sigov.pendencia_operacional')
and c.contype in('p','u','c','f') order by conname;
select indexname, indexdef from pg_indexes where schemaname='sigov' and tablename like 'compras_empresarial_%' order by 1;
select id, modulo, recurso, acao, ativo from sigov.permissao where recurso ilike '%compr%' or modulo ilike '%compr%' order by modulo,recurso,acao;
select count(*) n, count(distinct tenant_id) tenants, count(distinct entidade_id) entes from sigov.usuario;
select id, tenant_id, entidade_id, exercicio_id, login, ativo from sigov.usuario where id in (1,5,101,102,9001,9002) or tenant_id in (1,5) order by id limit 25;
