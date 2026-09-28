-- GATE UPGRADE PATH: cadeia legada minima e deterministica (pre-20260927).
-- Idempotente; usa ids explicitos altos para nao colidir com dados de baseline.
\set ON_ERROR_STOP on
begin;

insert into sigov.estoque_produto(id, tenant_id, sku, nome, unidade, estoque_minimo, permite_saldo_negativo, ativo)
values ('e9000000-0000-4000-8000-000000000001','b0000001-0000-4000-8000-000000000099','SKU-GATE-LEG-01','Produto legado Gate Upgrade','UN',0,false,true)
on conflict (tenant_id, sku) do nothing;

insert into sigov.compras_empresarial_pedido(id, tenant_id, numero, status, valor_total, total, created_at, created_by, updated_at, updated_by, correlation_id, version, is_deleted)
values ('a1000000-0000-4000-8000-000000000001','b0000001-0000-4000-8000-000000000099','PED-GATE-LEG-0001','APROVADO',6000,6000,now(),'gate-upgrade',now(),'gate-upgrade','gate-upgrade-correlation',1,false)
on conflict (id) do nothing;

insert into sigov.compras_empresarial_recebimento(id, tenant_id, pedido_id, documento, idempotency_key, resultado_inspecao, created_at, created_by, updated_at, updated_by, correlation_id, version, status, data_operacao)
values ('a2000000-0000-4000-8000-000000000001','b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','REC-GATE-LEG-0001','idm-gate-leg-0001','CONFORME',now(),'gate-upgrade',now(),'gate-upgrade','gate-upgrade-correlation',1,'CONCLUIDO',current_date)
on conflict (id) do nothing;

insert into sigov.compras_empresarial_pedido_item(id, tenant_id, pedido_id, produto_id, quantidade, quantidade_cancelada, valor_unitario, exige_inspecao)
overriding system value
values
 (990000001,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false),
 (990000002,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false),
 (990000003,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false),
 (990000004,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false),
 (990000005,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false),
 (990000006,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false),
 (990000007,'b0000001-0000-4000-8000-000000000099','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,1000,false)
on conflict (id) do nothing;

insert into sigov.compras_empresarial_recebimento_item(id, tenant_id, recebimento_id, pedido_item_id, produto_id, quantidade_fisica, quantidade_aceita, quantidade_rejeitada, quantidade_conferencia)
overriding system value
values
 (990000001,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000001,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
 (990000002,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000002,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
 (990000003,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000003,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
 (990000004,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000004,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
 (990000005,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000005,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
 (990000006,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000006,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
 (990000007,'b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000007,'e9000000-0000-4000-8000-000000000001',10,9,1,0)
on conflict (id) do nothing;

insert into sigov.compras_empresarial_recebimento_divergencia(tenant_id, recebimento_id, recebimento_item_id, quantidade_rejeitada, motivo, situacao, resultado, justificativa_encerramento, encerrada_em, version, created_at, updated_at, created_by, updated_by, correlation_id)
values
 ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000001,1,'Item com avaria irreparavel','ENCERRADA','DEVOLUCAO INTEGRAL DO ITEM 1','Legado A: devolvido integral ao fornecedor',now() - interval '7 days',1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation'),
 ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000002,1,'Divergencia de qualidade','ENCERRADA','GLOSA APLICADA CONFORME CONTRATO','Legado B: glosa formalizada',now() - interval '6 days',1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation'),
 ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000003,1,'Produto obsoleto','ENCERRADA','SUBSTITUICAO CONCLUIDA COM NOVO FORNECEDOR','Legado C: substituido',now() - interval '5 days',1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation'),
 ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000004,1,'Quantidade excedente','ENCERRADA','DEVOLUCAO PARCIAL DE 2 UNIDADES','Legado D: parte devolvida',now() - interval '4 days',1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation'),
 ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000005,1,'Texto legado livre','ENCERRADA','RETIFICADO PELO COMITE DE AQUISICOES (texto livre legado)','Legado E: padrao sem acento nao reconhecido',now() - interval '3 days',1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation'),
 ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000006,1,'Aguardando analise','ABERTA',null,null,null,1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation')
on conflict (tenant_id, recebimento_item_id) do nothing;

commit;

select 'FIXTURE_DIV' as s, recebimento_item_id::text, situacao, left(coalesce(resultado,'<null>'),60) from sigov.compras_empresarial_recebimento_divergencia where tenant_id='b0000001-0000-4000-8000-000000000099'::uuid order by recebimento_item_id;
select 'FIXTURE_COLS_NO_CODIGO' as s, count(*)::text from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo';
