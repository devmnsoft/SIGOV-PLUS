select 'RQ '||left(r.id::text,13)||' '||r.numero||' '||r.status||' v'||r.version from sigov.compras_empresarial_requisicao r where r.tenant_id='b0000001-0000-4000-8000-000000000001' order by r.numero;
select 'FORN='||count(*) from sigov.compras_empresarial_fornecedor where tenant_id='b0000001-0000-4000-8000-000000000001';
select 'COTACAO='||count(*) from sigov.compras_empresarial_cotacao where tenant_id='b0000001-0000-4000-8000-000000000001';
select 'PEDIDO='||count(*) from sigov.compras_empresarial_pedido where tenant_id='b0000001-0000-4000-8000-000000000001';
select 'PRODUTOS='||count(*) from sigov.estoque_produto;