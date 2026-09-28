-- GATE HEAL DEMO: simula o estado publicado P1 (coluna criada com varchar(24)) + linha legada com codigo que cabe em 24.
\set ON_ERROR_STOP on
begin;
alter table sigov.compras_empresarial_recebimento_divergencia add column resultado_codigo varchar(24);

insert into sigov.estoque_produto(id, tenant_id, sku, nome, unidade, estoque_minimo, permite_saldo_negativo, ativo)
values ('e9000000-0000-4000-8000-000000000001','b0000001-0000-4000-8000-000000000098','SKU-GATE-HEAL-01','Produto healing Gate','UN',0,false,true)
on conflict (tenant_id, sku) do nothing;

insert into sigov.compras_empresarial_pedido(id, tenant_id, numero, status, valor_total, total, created_at, created_by, updated_at, updated_by, correlation_id, version, is_deleted)
values ('a1000000-0000-4000-8000-000000000001','b0000001-0000-4000-8000-000000000098','PED-GATE-HEAL-0001','APROVADO',2000,2000,now(),'gate-heal',now(),'gate-heal','gate-heal-correlation',1,false)
on conflict (id) do nothing;

insert into sigov.compras_empresarial_recebimento(id, tenant_id, pedido_id, documento, idempotency_key, resultado_inspecao, created_at, created_by, updated_at, updated_by, correlation_id, version, status, data_operacao)
values ('a2000000-0000-4000-8000-000000000001','b0000001-0000-4000-8000-000000000098','a1000000-0000-4000-8000-000000000001','REC-GATE-HEAL-0001','idm-gate-heal-0001','CONFORME',now(),'gate-heal',now(),'gate-heal','gate-heal-correlation',1,'CONCLUIDO',current_date)
on conflict (id) do nothing;

insert into sigov.compras_empresarial_pedido_item(id, tenant_id, pedido_id, produto_id, quantidade, quantidade_cancelada, valor_unitario, exige_inspecao)
overriding system value
values (990000001,'b0000001-0000-4000-8000-000000000098','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,100,false),
       (990000002,'b0000001-0000-4000-8000-000000000098','a1000000-0000-4000-8000-000000000001','e9000000-0000-4000-8000-000000000001',10,0,100,false);

insert into sigov.compras_empresarial_recebimento_item(id, tenant_id, recebimento_id, pedido_item_id, produto_id, quantidade_fisica, quantidade_aceita, quantidade_rejeitada, quantidade_conferencia)
overriding system value
values (990000001,'b0000001-0000-4000-8000-000000000098','a2000000-0000-4000-8000-000000000001',990000001,'e9000000-0000-4000-8000-000000000001',10,9,1,0),
       (990000002,'b0000001-0000-4000-8000-000000000098','a2000000-0000-4000-8000-000000000001',990000002,'e9000000-0000-4000-8000-000000000001',10,9,1,0);

-- Linha legada 1: ENCERRADA com codigo de 14 chars (cabe em varchar(24)); sobrevive a backfills.
insert into sigov.compras_empresarial_recebimento_divergencia(tenant_id, recebimento_id, recebimento_item_id, quantidade_rejeitada, motivo, situacao, resultado_codigo, resultado, justificativa_encerramento, encerrada_em, version, created_at, updated_at, created_by, updated_by, correlation_id)
values ('b0000001-0000-4000-8000-000000000098','a2000000-0000-4000-8000-000000000001',990000001,1,'Glosa legada','ENCERRADA','GLOSA_APLICADA','GLOSA APLICADA NO CONTRATO LEGADO','Legado com codigo ja presente',now() - interval '3 days',1,now(),now(),'gate-heal','gate-heal','gate-heal-correlation');

commit;
select 'SIM_LEN' as s, character_maximum_length::text from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo';
select 'SIM_ROW' as s, coalesce(resultado_codigo,'<null>') from sigov.compras_empresarial_recebimento_divergencia where tenant_id='b0000001-0000-4000-8000-000000000098'::uuid;
