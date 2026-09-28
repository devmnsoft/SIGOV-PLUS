\set ON_ERROR_STOP on
insert into sigov.compras_empresarial_recebimento_divergencia(tenant_id, recebimento_id, recebimento_item_id, quantidade_rejeitada, motivo, situacao, resultado_codigo, resultado, justificativa_encerramento, encerrada_em, version, created_at, updated_at, created_by, updated_by, correlation_id)
values ('b0000001-0000-4000-8000-000000000099','a2000000-0000-4000-8000-000000000001',990000007,1,'Tentativa de ENCERRADA sem codigo','ENCERRADA',null,'RESULTADO LEGADO QUALQUER','justificativa de teste',now(),1,now(),now(),'gate-upgrade','gate-upgrade','gate-upgrade-correlation');
