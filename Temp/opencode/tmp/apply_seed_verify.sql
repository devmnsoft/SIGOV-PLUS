select 'FORN_TOTAL='||count(*) from sigov.compras_empresarial_fornecedor where tenant_id='b0000001-0000-4000-8000-000000000001';
select f.codigo||' '||f.status||' ativo='||f.ativo from sigov.compras_empresarial_fornecedor f where f.tenant_id='b0000001-0000-4000-8000-000000000001' order by f.codigo;
select 'HIST_FORN_CRIADO='||count(*) from sigov.compras_empresarial_historico h where h.tenant_id='b0000001-0000-4000-8000-000000000001' and h.aggregate_type='FORNECEDOR' and h.acao='CRIADO';
select 'PP_9001='||count(*) from sigov.perfil_permissao where perfil_acesso_id=9001;
select 'PP_9002='||count(*) from sigov.perfil_permissao where perfil_acesso_id=9002;