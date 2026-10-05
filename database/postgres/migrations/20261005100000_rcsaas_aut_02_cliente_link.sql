-- ============================================================
-- RC-SAAS-AUT | 20261005100000
-- Vinculo do master comercial (saas_cliente) ao tenant.
--
-- A familia E (saas_cliente) referencia o tenant sem FK; este
-- passo garante integridade referencial do registro comercial
-- multi-esfera. A tabela esta vazia em dev/homologacao e o NOT
-- NULL e reassertido apenas enquanto vazia (guarda idempotente).
-- Idempotente; sem dados pessoais; sem senha/chave literal.
-- ============================================================

do $$
begin
    if not exists (
        select 1
        from pg_constraint c
        join pg_class t on t.oid = c.conrelid
        where c.conname = 'fk_saas_cliente_tenant'
          and t.relname = 'saas_cliente'
    ) then
        alter table sigov.saas_cliente
            add constraint fk_saas_cliente_tenant
            foreign key (tenant_id) references sigov.tenant(id);
    end if;
end $$;

do $$
begin
    if (select count(*) from sigov.saas_cliente) = 0 then
        alter table sigov.saas_cliente alter column tenant_id set not null;
    end if;
end $$;
