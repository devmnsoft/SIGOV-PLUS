select id, codigo, nome, status, created_at, updated_at from sigov.compras_empresarial_fornecedor where tenant_id='b0000001-0000-4000-8000-000000000001' order by created_at;
select id, acao, aggregate_type, aggregate_id, occurred_at from sigov.compras_empresarial_historico where tenant_id='b0000001-0000-4000-8000-000000000001' and aggregate_type='FORNECEDOR' and acao='CRIADO' order by occurred_at;
select id, numero, status, fornecedor_id, requisicao_id, cotacao_id, created_at from sigov.compras_empresarial_pedido where tenant_id='b0000001-0000-4000-8000-000000000001';
select chave, resultado::text is null as tem_resultado, created_at from sigov.compras_empresarial_idempotencia where tenant_id='b0000001-0000-4000-8000-000000000001' and chave like 'jb-%' order by chave;
