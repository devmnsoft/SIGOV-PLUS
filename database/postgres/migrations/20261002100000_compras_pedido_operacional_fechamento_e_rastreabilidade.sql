-- Fechamento operacional do pedido, rastreabilidade ponta a ponta por item
-- (requisicao_item -> cotacao_item/selecao -> pedido_item) e composicao
-- monetaria reconciliavel (bruto, desconto, imposto, frete, liquido, total).
-- Multi-esfera: compativel com municipios, estados e Uniao. Idempotente.

alter table sigov.compras_empresarial_pedido_item
    add column if not exists cotacao_item_id bigint references sigov.compras_empresarial_cotacao_item(id),
    add column if not exists cotacao_selecao_id bigint references sigov.compras_empresarial_cotacao_selecao(id),
    add column if not exists requisicao_item_id uuid references sigov.compras_empresarial_requisicao_item(id),
    add column if not exists valor_bruto numeric(14,4) not null default 0,
    add column if not exists desconto numeric(14,4) not null default 0,
    add column if not exists imposto numeric(14,4) not null default 0,
    add column if not exists frete numeric(14,4) not null default 0,
    add column if not exists valor_liquido numeric(14,4) not null default 0,
    add column if not exists total_item numeric(14,4) not null default 0;

create index if not exists ix_ce_pedido_item_cotacao_item
    on sigov.compras_empresarial_pedido_item(tenant_id, cotacao_item_id);

create index if not exists ix_ce_pedido_item_cotacao_selecao
    on sigov.compras_empresarial_pedido_item(tenant_id, cotacao_selecao_id);

create index if not exists ix_ce_pedido_item_requisicao_item
    on sigov.compras_empresarial_pedido_item(tenant_id, requisicao_item_id);

alter table sigov.compras_empresarial_pedido
    add column if not exists valor_bruto numeric(14,4) not null default 0,
    add column if not exists desconto_total numeric(14,4) not null default 0,
    add column if not exists imposto_total numeric(14,4) not null default 0,
    add column if not exists frete_total numeric(14,4) not null default 0,
    add column if not exists valor_liquido numeric(14,4) not null default 0,
    add column if not exists motivo_encerramento text,
    add column if not exists encerrado_em timestamptz,
    add column if not exists encerrado_por varchar(100),
    add column if not exists motivo_cancelamento text,
    add column if not exists cancelado_em timestamptz,
    add column if not exists cancelado_por varchar(100);

alter table sigov.compras_empresarial_recebimento_divergencia
    add column if not exists reposicao_autorizada boolean not null default false,
    add column if not exists quantidade_reposicao numeric(14,4) not null default 0,
    add column if not exists reposicao_recebida numeric(14,4) not null default 0;

alter table sigov.compras_empresarial_recebimento_item
    add column if not exists divergencia_origem_id bigint references sigov.compras_empresarial_recebimento_divergencia(id);

create index if not exists ix_ce_recebimento_item_divergencia_origem
    on sigov.compras_empresarial_recebimento_item(tenant_id, divergencia_origem_id);

-- Backfill idempotente de dados legados
update sigov.compras_empresarial_pedido_item
set total_item = round(quantidade * valor_unitario, 2),
    valor_liquido = round(quantidade * valor_unitario, 2),
    valor_bruto = round(quantidade * valor_unitario, 2)
where total_item = 0 and valor_unitario > 0;

update sigov.compras_empresarial_pedido
set valor_bruto = coalesce(nullif(valor_total, 0), total),
    valor_liquido = coalesce(nullif(valor_total, 0), total)
where valor_bruto = 0 and (valor_total > 0 or total > 0);
