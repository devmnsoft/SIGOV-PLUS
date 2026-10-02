create schema if not exists sigov;
create extension if not exists pgcrypto;

create or replace function sigov.enterprise_ensure_table(p_table text) returns void language plpgsql as $$
begin
  execute format('create table if not exists sigov.%I (id uuid primary key default gen_random_uuid(), tenant_id uuid not null, codigo text not null, nome text not null, titulo text null, status text not null default ''ATIVO'', documento_masked text null, email_masked text null, telefone_masked text null, dados_json jsonb not null default ''{}''::jsonb, created_at timestamptz not null default now(), created_by text null, updated_at timestamptz not null default now(), updated_by text null, deleted_at timestamptz null, deleted_by text null, is_deleted boolean not null default false, correlation_id text null)', p_table);
  execute format('alter table sigov.%I add column if not exists tenant_id uuid not null default ''00000000-0000-0000-0000-000000000000''::uuid', p_table);
  execute format('alter table sigov.%I add column if not exists codigo text not null default ''''', p_table);
  execute format('alter table sigov.%I add column if not exists nome text not null default ''''', p_table);
  execute format('alter table sigov.%I add column if not exists titulo text null', p_table);
  execute format('alter table sigov.%I add column if not exists status text not null default ''ATIVO''', p_table);
  execute format('alter table sigov.%I add column if not exists documento_masked text null', p_table);
  execute format('alter table sigov.%I add column if not exists email_masked text null', p_table);
  execute format('alter table sigov.%I add column if not exists telefone_masked text null', p_table);
  execute format('alter table sigov.%I add column if not exists dados_json jsonb not null default ''{}''::jsonb', p_table);
  execute format('alter table sigov.%I add column if not exists created_at timestamptz not null default now()', p_table);
  execute format('alter table sigov.%I add column if not exists created_by text null', p_table);
  execute format('alter table sigov.%I add column if not exists updated_at timestamptz not null default now()', p_table);
  execute format('alter table sigov.%I add column if not exists updated_by text null', p_table);
  execute format('alter table sigov.%I add column if not exists deleted_at timestamptz null', p_table);
  execute format('alter table sigov.%I add column if not exists deleted_by text null', p_table);
  execute format('alter table sigov.%I add column if not exists is_deleted boolean not null default false', p_table);
  execute format('alter table sigov.%I add column if not exists correlation_id text null', p_table);
  execute format('create index if not exists %I on sigov.%I(tenant_id)', p_table || '_tenant_idx', p_table);
  execute format('create index if not exists %I on sigov.%I(status)', p_table || '_status_idx', p_table);
  execute format('create index if not exists %I on sigov.%I(codigo)', p_table || '_codigo_idx', p_table);
  execute format('create index if not exists %I on sigov.%I(created_at)', p_table || '_created_idx', p_table);
end $$;

do $$
declare t text;
begin
  foreach t in array array[
    'enterprise_cliente','enterprise_lead','enterprise_oportunidade','enterprise_proposta','enterprise_proposta_item','enterprise_pedido_venda','enterprise_pedido_venda_item','enterprise_tabela_preco','enterprise_comissao',
    'enterprise_ordem_servico','enterprise_os_item','enterprise_os_checklist','enterprise_os_apontamento','enterprise_os_agenda','enterprise_os_historico',
    'enterprise_produto','enterprise_almoxarifado','enterprise_estoque_saldo','enterprise_estoque_movimento','enterprise_requisicao','enterprise_requisicao_item','enterprise_fornecedor','enterprise_pedido_compra','enterprise_pedido_compra_item',
    'enterprise_ativo_industrial','enterprise_plano_manutencao','enterprise_medidor','enterprise_leitura_medidor','enterprise_parada_falha',
    'enterprise_centro_trabalho','enterprise_recurso_produtivo','enterprise_produto_industrial','enterprise_ficha_tecnica','enterprise_ficha_tecnica_item','enterprise_roteiro_producao','enterprise_ordem_producao','enterprise_apontamento_producao','enterprise_inspecao_qualidade','enterprise_custo_producao',
    'enterprise_evento','enterprise_auditoria_operacional'] loop
    perform sigov.enterprise_ensure_table(t);
  end loop;
end $$;

alter table sigov.enterprise_estoque_saldo add column if not exists produto_id uuid;
alter table sigov.enterprise_estoque_saldo add column if not exists produto_nome text;
alter table sigov.enterprise_estoque_saldo add column if not exists quantidade numeric(18,4) not null default 0;
alter table sigov.enterprise_estoque_saldo add column if not exists minimo numeric(18,4) not null default 0;
create unique index if not exists enterprise_estoque_saldo_tenant_produto_uidx on sigov.enterprise_estoque_saldo(tenant_id, produto_id) where is_deleted=false;
