-- Reserva e saldo faturável, conferência avançada de faturas e rastreabilidade por item.
-- Multi-esfera: municípios, estados e União. Idempotente.

alter table sigov.compras_empresarial_fatura_item
    add column if not exists quantidade_reservada numeric(18,4) not null default 0,
    add column if not exists quantidade_aprovada numeric(18,4) not null default 0,
    add column if not exists status_item varchar(30) not null default 'EM_CONFERENCIA',
    add column if not exists diagnostico text null,
    add column if not exists valor_unitario_pedido numeric(18,4) null;

do $$
begin
    if not exists (select 1 from pg_constraint where conname = 'ck_ce_fatura_item_reservada' and conrelid = 'sigov.compras_empresarial_fatura_item'::regclass) then
        alter table sigov.compras_empresarial_fatura_item add constraint ck_ce_fatura_item_reservada check (quantidade_reservada >= 0);
    end if;
    if not exists (select 1 from pg_constraint where conname = 'ck_ce_fatura_item_aprovada' and conrelid = 'sigov.compras_empresarial_fatura_item'::regclass) then
        alter table sigov.compras_empresarial_fatura_item add constraint ck_ce_fatura_item_aprovada check (quantidade_aprovada >= 0);
    end if;
end $$;

-- Backfill idempotente de itens existentes baseado no status da fatura pai
update sigov.compras_empresarial_fatura_item fi
set quantidade_aprovada = fi.quantidade,
    quantidade_reservada = 0,
    status_item = 'APROVADA'
from sigov.compras_empresarial_fatura f
where f.id = fi.fatura_id and f.tenant_id = fi.tenant_id
  and f.status = 'APROVADA'
  and fi.quantidade_aprovada = 0 and fi.quantidade_reservada = 0 and fi.status_item = 'EM_CONFERENCIA';

update sigov.compras_empresarial_fatura_item fi
set quantidade_aprovada = 0,
    quantidade_reservada = 0,
    status_item = f.status
from sigov.compras_empresarial_fatura f
where f.id = fi.fatura_id and f.tenant_id = fi.tenant_id
  and f.status in ('REJEITADA', 'CANCELADA')
  and fi.status_item = 'EM_CONFERENCIA';

update sigov.compras_empresarial_fatura_item fi
set quantidade_aprovada = 0,
    quantidade_reservada = fi.quantidade,
    status_item = case when f.resultado_match = 'MATCH_TOTAL' then 'CONFORME' else 'COM_DIVERGENCIA' end
from sigov.compras_empresarial_fatura f
where f.id = fi.fatura_id and f.tenant_id = fi.tenant_id
  and f.status in ('EM_CONFERENCIA', 'COM_DIVERGENCIA', 'CONFORME')
  and fi.quantidade_reservada = 0 and fi.quantidade_aprovada = 0 and fi.status_item = 'EM_CONFERENCIA';

update sigov.compras_empresarial_fatura_item fi
set valor_unitario_pedido = pi.valor_unitario
from sigov.compras_empresarial_pedido_item pi
where pi.id = fi.pedido_item_id and pi.tenant_id = fi.tenant_id
  and fi.valor_unitario_pedido is null;
