-- Correcao aditiva: tabela base do outbox transacional de integracao
-- (sigov.integracao_outbox) usada pelos repositorios compras-empresariais
-- (fornecedor criado) e OS por pedido para publicacao transacional de
-- eventos ao lado do agregado. Sem regra municipal hardcoded; multi-esfera
-- por tenant. Idempotente (reexecucao segura).

create table if not exists sigov.integracao_outbox (
    id uuid not null primary key default gen_random_uuid(),
    tenant_id uuid not null,
    tipo_evento varchar(120) not null check (btrim(tipo_evento) <> ''),
    aggregate_id uuid not null,
    payload jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now()
);

create index if not exists ix_integracao_outbox_tenant_tipo
    on sigov.integracao_outbox(tenant_id, tipo_evento, created_at);

comment on table sigov.integracao_outbox is 'Outbox transacional de eventos de integracao (compras-empresariais e OS por pedido); consumo por worker oficial.';
