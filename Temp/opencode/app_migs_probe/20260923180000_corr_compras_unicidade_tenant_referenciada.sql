-- Correção aditiva e idempotente: garante a unicidade multi-tenant (tenant_id, id)
-- nas tabelas canônicas pai das FKs compostas publicadas em 20260924120000
-- (fk_comp_recb_div_recb, fk_comp_recb_div_item) e 20260924210000
-- (fk_comp_dev_receb, fk_comp_dev_item_origem), que referenciam
-- sigov.compras_empresarial_recebimento(tenant_id,id) e
-- sigov.compras_empresarial_recebimento_item(tenant_id,id) sem que essas
-- unicidades existissem na época da aplicação daquelas migrations.
-- Os índices usam nomes distintos dos constraints criados pela migration publicada
-- 20260924160000 (ux_comp_recb_tenant_id, ux_comp_recb_item_parent), porque o
-- PostgreSQL não promove índice avulso pré-existente a constraint pelo nome;
-- a unicidade redundante sobre (id) é intencional e sem efeito em dados legítimos.

do $$
begin
 create unique index if not exists ux_comp_recb_tenant_fk on sigov.compras_empresarial_recebimento(tenant_id, id);
exception when undefined_table then
 raise exception 'Tabela canônica sigov.compras_empresarial_recebimento ausente: unicidade multi-tenant não pode ser garantida.';
end $$;

do $$
begin
 create unique index if not exists ux_comp_recb_item_tenant_fk on sigov.compras_empresarial_recebimento_item(tenant_id, id);
exception when undefined_table then
 raise exception 'Tabela canônica sigov.compras_empresarial_recebimento_item ausente: unicidade multi-tenant não pode ser garantida.';
end $$;
