-- Jornada canônica de recebimento parcial de compras empresariais.
create table if not exists sigov.compras_empresarial_pedido_item(
 id bigint generated always as identity primary key,
 tenant_id uuid not null,
 pedido_id uuid not null references sigov.compras_empresarial_pedido(id),
 produto_id uuid not null references sigov.estoque_produto(id),
 quantidade numeric(14,4) not null check(quantidade>0),
 quantidade_cancelada numeric(14,4) not null default 0,
 valor_unitario numeric(14,4) not null default 0,
 exige_inspecao boolean not null default false,
 constraint ux_compras_empresarial_pedido_item unique(tenant_id,pedido_id,id)
);

do $$ begin
 if not exists(select 1 from pg_constraint where conname='ck_compras_pedido_item_cancelada' and conrelid='sigov.compras_empresarial_pedido_item'::regclass) then
  alter table sigov.compras_empresarial_pedido_item add constraint ck_compras_pedido_item_cancelada check(quantidade_cancelada>=0 and quantidade_cancelada<=quantidade);
 end if;
end $$;

alter table sigov.compras_empresarial_recebimento
 add column if not exists status varchar(24) not null default 'RASCUNHO',
 add column if not exists almoxarifado_id uuid references sigov.estoque_almoxarifado(id),
 add column if not exists data_operacao timestamptz not null default now(),
 add column if not exists observacoes text;

create table if not exists sigov.compras_empresarial_recebimento_item(
 id bigint generated always as identity primary key,
 tenant_id uuid not null,
 recebimento_id uuid not null references sigov.compras_empresarial_recebimento(id),
 pedido_item_id bigint not null references sigov.compras_empresarial_pedido_item(id),
 produto_id uuid not null references sigov.estoque_produto(id),
 quantidade_fisica numeric(14,4) not null,
 quantidade_aceita numeric(14,4) not null default 0,
 quantidade_rejeitada numeric(14,4) not null default 0,
 quantidade_conferencia numeric(14,4) not null default 0,
 lote varchar(100), validade date, numero_serie varchar(150),
 constraint ck_compras_recebimento_item_quantidades check(quantidade_fisica>0 and quantidade_aceita>=0 and quantidade_rejeitada>=0 and quantidade_conferencia>=0 and quantidade_aceita+quantidade_rejeitada+quantidade_conferencia=quantidade_fisica),
 constraint ux_compras_recebimento_item unique(tenant_id,recebimento_id,pedido_item_id)
);
create index if not exists ix_compras_recebimento_item_pedido on sigov.compras_empresarial_recebimento_item(tenant_id,pedido_item_id,id);

create table if not exists sigov.compras_empresarial_recebimento_evento(
 id bigint generated always as identity primary key,
 tenant_id uuid not null,
 recebimento_id uuid not null references sigov.compras_empresarial_recebimento(id),
 tipo varchar(40) not null, detalhes jsonb not null default '{}'::jsonb,
 usuario_id uuid not null, correlation_id varchar(100) not null,
 ocorrido_em timestamptz not null default now()
);
create index if not exists ix_compras_recebimento_evento_timeline on sigov.compras_empresarial_recebimento_evento(tenant_id,recebimento_id,ocorrido_em,id);
create index if not exists ix_compras_recebimento_central on sigov.compras_empresarial_recebimento(tenant_id,status,data_operacao desc,id);
