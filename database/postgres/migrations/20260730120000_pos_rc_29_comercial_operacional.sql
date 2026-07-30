-- Pós-RC 29: bounded context Comercial, incremental e compatível com tabelas enterprise históricas.
create extension if not exists pgcrypto;

do $$ begin
  alter table sigov.enterprise_cliente add column if not exists tipo_pessoa text not null default 'J';
  alter table sigov.enterprise_cliente add column if not exists nome_fantasia text;
  alter table sigov.enterprise_cliente add column if not exists segmento text;
  alter table sigov.enterprise_cliente add column if not exists origem text;
  alter table sigov.enterprise_cliente add column if not exists documento_hash text;
  alter table sigov.enterprise_cliente add column if not exists documento_protegido text;
  alter table sigov.enterprise_cliente add column if not exists email_protegido text;
  alter table sigov.enterprise_cliente add column if not exists telefone_protegido text;
  alter table sigov.enterprise_cliente add column if not exists responsavel_usuario_id uuid;
  alter table sigov.enterprise_cliente add column if not exists version bigint not null default 1;

  alter table sigov.enterprise_lead add column if not exists origem text;
  alter table sigov.enterprise_lead add column if not exists interesse text;
  alter table sigov.enterprise_lead add column if not exists pontuacao integer not null default 0;
  alter table sigov.enterprise_lead add column if not exists proximo_contato_em timestamptz;
  alter table sigov.enterprise_lead add column if not exists responsavel_usuario_id uuid;
  alter table sigov.enterprise_lead add column if not exists cliente_convertido_id uuid;
  alter table sigov.enterprise_lead add column if not exists oportunidade_convertida_id uuid;
  alter table sigov.enterprise_lead add column if not exists convertido_em timestamptz;
  alter table sigov.enterprise_lead add column if not exists descartado_motivo text;
  alter table sigov.enterprise_lead add column if not exists documento_hash text;
  alter table sigov.enterprise_lead add column if not exists documento_protegido text;
  alter table sigov.enterprise_lead add column if not exists email_protegido text;
  alter table sigov.enterprise_lead add column if not exists telefone_protegido text;
  alter table sigov.enterprise_lead add column if not exists version bigint not null default 1;

  alter table sigov.enterprise_oportunidade add column if not exists cliente_id uuid;
  alter table sigov.enterprise_oportunidade add column if not exists lead_id uuid;
  alter table sigov.enterprise_oportunidade add column if not exists fase text not null default 'PROSPECCAO';
  alter table sigov.enterprise_oportunidade add column if not exists valor_estimado numeric(18,2) not null default 0;
  alter table sigov.enterprise_oportunidade add column if not exists probabilidade integer not null default 0;
  alter table sigov.enterprise_oportunidade add column if not exists previsao_fechamento date;
  alter table sigov.enterprise_oportunidade add column if not exists responsavel_usuario_id uuid;
  alter table sigov.enterprise_oportunidade add column if not exists motivo_perda text;
  alter table sigov.enterprise_oportunidade add column if not exists ganho_em timestamptz;
  alter table sigov.enterprise_oportunidade add column if not exists perdido_em timestamptz;
  alter table sigov.enterprise_oportunidade add column if not exists version bigint not null default 1;

  alter table sigov.enterprise_proposta add column if not exists numero text;
  alter table sigov.enterprise_proposta add column if not exists cliente_id uuid;
  alter table sigov.enterprise_proposta add column if not exists oportunidade_id uuid;
  alter table sigov.enterprise_proposta add column if not exists validade_em date;
  alter table sigov.enterprise_proposta add column if not exists subtotal numeric(18,2) not null default 0;
  alter table sigov.enterprise_proposta add column if not exists desconto numeric(18,2) not null default 0;
  alter table sigov.enterprise_proposta add column if not exists acrescimo numeric(18,2) not null default 0;
  alter table sigov.enterprise_proposta add column if not exists total numeric(18,2) not null default 0;
  alter table sigov.enterprise_proposta add column if not exists condicoes_pagamento text;
  alter table sigov.enterprise_proposta add column if not exists observacao text;
  alter table sigov.enterprise_proposta add column if not exists emitida_em timestamptz;
  alter table sigov.enterprise_proposta add column if not exists decidida_em timestamptz;
  alter table sigov.enterprise_proposta add column if not exists pedido_id uuid;
  alter table sigov.enterprise_proposta add column if not exists version bigint not null default 1;

  alter table sigov.enterprise_proposta_item add column if not exists proposta_id uuid;
  alter table sigov.enterprise_proposta_item add column if not exists produto_id uuid;
  alter table sigov.enterprise_proposta_item add column if not exists descricao text;
  alter table sigov.enterprise_proposta_item add column if not exists unidade text not null default 'UN';
  alter table sigov.enterprise_proposta_item add column if not exists quantidade numeric(18,4) not null default 1;
  alter table sigov.enterprise_proposta_item add column if not exists valor_unitario numeric(18,4) not null default 0;
  alter table sigov.enterprise_proposta_item add column if not exists desconto numeric(18,2) not null default 0;
  alter table sigov.enterprise_proposta_item add column if not exists total numeric(18,2) not null default 0;
  alter table sigov.enterprise_proposta_item add column if not exists ordem integer not null default 0;
  alter table sigov.enterprise_proposta_item add column if not exists version bigint not null default 1;

  alter table sigov.enterprise_pedido_venda add column if not exists numero text;
  alter table sigov.enterprise_pedido_venda add column if not exists proposta_id uuid;
  alter table sigov.enterprise_pedido_venda add column if not exists cliente_id uuid;
  alter table sigov.enterprise_pedido_venda add column if not exists subtotal numeric(18,2) not null default 0;
  alter table sigov.enterprise_pedido_venda add column if not exists desconto numeric(18,2) not null default 0;
  alter table sigov.enterprise_pedido_venda add column if not exists total numeric(18,2) not null default 0;
  alter table sigov.enterprise_pedido_venda add column if not exists previsao_entrega date;
  alter table sigov.enterprise_pedido_venda add column if not exists confirmado_em timestamptz;
  alter table sigov.enterprise_pedido_venda add column if not exists concluido_em timestamptz;
  alter table sigov.enterprise_pedido_venda add column if not exists cancelado_em timestamptz;
  alter table sigov.enterprise_pedido_venda add column if not exists cancelamento_motivo text;
  alter table sigov.enterprise_pedido_venda add column if not exists requer_ordem_servico boolean not null default false;
  alter table sigov.enterprise_pedido_venda add column if not exists ordem_servico_id uuid;
  alter table sigov.enterprise_pedido_venda add column if not exists version bigint not null default 1;

  alter table sigov.enterprise_pedido_venda_item add column if not exists pedido_id uuid;
  alter table sigov.enterprise_pedido_venda_item add column if not exists produto_id uuid;
  alter table sigov.enterprise_pedido_venda_item add column if not exists descricao text;
  alter table sigov.enterprise_pedido_venda_item add column if not exists unidade text not null default 'UN';
  alter table sigov.enterprise_pedido_venda_item add column if not exists quantidade numeric(18,4) not null default 1;
  alter table sigov.enterprise_pedido_venda_item add column if not exists valor_unitario numeric(18,4) not null default 0;
  alter table sigov.enterprise_pedido_venda_item add column if not exists desconto numeric(18,2) not null default 0;
  alter table sigov.enterprise_pedido_venda_item add column if not exists total numeric(18,2) not null default 0;
  alter table sigov.enterprise_pedido_venda_item add column if not exists reservar_estoque boolean not null default false;
end $$;

create table if not exists sigov.enterprise_interacao_comercial (id uuid primary key default gen_random_uuid(), tenant_id uuid not null, entidade_tipo text not null, entidade_id uuid not null, tipo text not null, descricao text not null, proxima_atividade_em timestamptz, created_at timestamptz not null default now(), created_by text, updated_at timestamptz not null default now(), updated_by text, correlation_id text not null, version bigint not null default 1, is_deleted boolean not null default false);
create table if not exists sigov.enterprise_status_historico (id uuid primary key default gen_random_uuid(), tenant_id uuid not null, entidade_tipo text not null, entidade_id uuid not null, status_anterior text, status_novo text not null, observacao text, created_at timestamptz not null default now(), created_by text, correlation_id text not null, version bigint not null default 1, is_deleted boolean not null default false);
create table if not exists sigov.enterprise_estoque_reserva (id uuid primary key default gen_random_uuid(), tenant_id uuid not null, pedido_id uuid not null, pedido_item_id uuid not null, produto_id uuid not null, quantidade numeric(18,4) not null check (quantidade > 0), status text not null default 'ATIVA', created_at timestamptz not null default now(), created_by text, updated_at timestamptz not null default now(), updated_by text, correlation_id text not null, version bigint not null default 1, is_deleted boolean not null default false);
create table if not exists sigov.enterprise_vinculo_documento (id uuid primary key default gen_random_uuid(), tenant_id uuid not null, entidade_tipo text not null, entidade_id uuid not null, documento_id uuid not null, created_at timestamptz not null default now(), created_by text, updated_at timestamptz not null default now(), updated_by text, correlation_id text not null, version bigint not null default 1, is_deleted boolean not null default false);
create table if not exists sigov.enterprise_operacao_idempotente (id uuid primary key default gen_random_uuid(), tenant_id uuid not null, operacao text not null, chave text not null, aggregate_id uuid not null, created_at timestamptz not null default now(), created_by text, correlation_id text not null);

create unique index if not exists enterprise_cliente_documento_hash_uidx on sigov.enterprise_cliente(tenant_id, documento_hash) where documento_hash is not null and is_deleted=false;
create unique index if not exists enterprise_proposta_numero_uidx on sigov.enterprise_proposta(tenant_id, numero) where numero is not null and is_deleted=false;
create unique index if not exists enterprise_pedido_numero_uidx on sigov.enterprise_pedido_venda(tenant_id, numero) where numero is not null and is_deleted=false;
create unique index if not exists enterprise_pedido_proposta_uidx on sigov.enterprise_pedido_venda(tenant_id, proposta_id) where proposta_id is not null and is_deleted=false;
create unique index if not exists enterprise_operacao_idempotente_uidx on sigov.enterprise_operacao_idempotente(tenant_id, operacao, chave);
create index if not exists enterprise_lead_operacao_idx on sigov.enterprise_lead(tenant_id, status, responsavel_usuario_id, proximo_contato_em) where is_deleted=false;
create index if not exists enterprise_oportunidade_funil_idx on sigov.enterprise_oportunidade(tenant_id, fase, responsavel_usuario_id, previsao_fechamento) where is_deleted=false;
create index if not exists enterprise_proposta_operacao_idx on sigov.enterprise_proposta(tenant_id, status, validade_em) where is_deleted=false;
create index if not exists enterprise_pedido_operacao_idx on sigov.enterprise_pedido_venda(tenant_id, status, previsao_entrega) where is_deleted=false;
create index if not exists enterprise_status_historico_entidade_idx on sigov.enterprise_status_historico(tenant_id, entidade_tipo, entidade_id, created_at desc);
create index if not exists enterprise_vinculo_documento_entidade_idx on sigov.enterprise_vinculo_documento(tenant_id, entidade_tipo, entidade_id) where is_deleted=false;

insert into sigov.permissao(modulo,recurso,acao,chave,descricao,ativo)
select 'COMERCIAL', split_part(chave,'.',2), split_part(chave,'.',3), chave, descricao, true from (values
 ('comercial.dashboard.visualizar','Visualizar dashboard comercial'),('comercial.clientes.visualizar','Visualizar clientes'),('comercial.clientes.criar','Criar clientes'),('comercial.clientes.editar','Editar clientes'),('comercial.clientes.inativar','Inativar clientes'),('comercial.clientes.dados_pessoais.visualizar','Visualizar dados pessoais de clientes'),('comercial.leads.visualizar','Visualizar leads'),('comercial.leads.criar','Criar leads'),('comercial.leads.editar','Editar leads'),('comercial.leads.qualificar','Qualificar leads'),('comercial.leads.converter','Converter leads'),('comercial.oportunidades.visualizar','Visualizar oportunidades'),('comercial.oportunidades.criar','Criar oportunidades'),('comercial.oportunidades.editar','Editar oportunidades'),('comercial.oportunidades.mover_fase','Mover oportunidades'),('comercial.propostas.visualizar','Visualizar propostas'),('comercial.propostas.criar','Criar propostas'),('comercial.propostas.editar','Editar propostas'),('comercial.propostas.emitir','Emitir propostas'),('comercial.propostas.aprovar','Aprovar propostas'),('comercial.propostas.reprovar','Reprovar propostas'),('comercial.propostas.gerar_pedido','Gerar pedidos'),('comercial.pedidos.visualizar','Visualizar pedidos'),('comercial.pedidos.confirmar','Confirmar pedidos'),('comercial.pedidos.cancelar','Cancelar pedidos'),('comercial.pedidos.gerar_os','Gerar ordem de serviço'),('comercial.exportar','Exportar dados comerciais'),('comercial.importar','Importar dados comerciais')
) p(chave,descricao) on conflict(chave) do update set descricao=excluded.descricao,ativo=true;
