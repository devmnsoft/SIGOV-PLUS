select table_name, column_name, data_type, character_maximum_length, is_nullable, coalesce(column_default,'') dft
from information_schema.columns
where table_schema='sigov' and table_name in (
 'compras_empresarial_cotacao','compras_empresarial_cotacao_convite','compras_empresarial_cotacao_resposta_item',
 'compras_empresarial_pedido','compras_empresarial_pedido_item','compras_empresarial_requisicao_item',
 'compras_empresarial_idempotencia','compras_empresarial_aprovacao','compras_empresarial_fornecedor',
 'usuario','estoque_produto','enterprise_tenant_mapping')
order by table_name, ordinal_position;
select conname, pg_get_constraintdef(oid) def
from pg_constraint c join pg_namespace n on n.oid=c.connamespace
where n.nspname='sigov' and c.conrelid::regclass::text in (
 'sigov.compras_empresarial_cotacao','sigov.compras_empresarial_cotacao_convite','sigov.compras_empresarial_cotacao_resposta_item',
 'sigov.compras_empresarial_pedido','sigov.compras_empresarial_idempotencia','sigov.compras_empresarial_aprovacao','sigov.compras_empresarial_recebimento')
and c.contype in('p','u','c','f') order by 1;
select indexname, indexdef from pg_indexes where schemaname='sigov' and tablename like 'compras_empresarial_%' order by 1;
select id, modulo, recurso, acao, ativo from sigov.permissao where recurso ilike '%compr%' or modulo ilike '%compr%' order by modulo,recurso,acao;
select id, nome, ativo, tenant_id, entidade_id, exercicio_id, unidade_id from sigov.usuario order by id limit 30;
select count(*) usuarios, count(distinct tenant_id) tenants, count(distinct entidade_id) entes, count(distinct exercicio_id) exs, count(distinct unidade_id) unis from sigov.usuario;
