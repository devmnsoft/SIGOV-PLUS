-- GATE HEAL DEMO: reproduz o bug P1 — codigo de 27 chars em coluna varchar(24) deve falhar ANTES do heal.
\set ON_ERROR_STOP on
insert into sigov.compras_empresarial_recebimento_divergencia(tenant_id, recebimento_id, recebimento_item_id, quantidade_rejeitada, motivo, situacao, resultado_codigo, resultado, justificativa_encerramento, encerrada_em, version, created_at, updated_at, created_by, updated_by, correlation_id)
values ('b0000001-0000-4000-8000-000000000098','a2000000-0000-4000-8000-000000000001',990000002,1,'Encerramento administrativo legado','ENCERRADA','ENCERRAMENTO_ADMINISTRATIVO','RETIFICACAO ADMINISTRATIVA LEGADA','Legado sem padrao reconhecido',now() - interval '2 days',1,now(),now(),'gate-heal','gate-heal','gate-heal-correlation');
