-- Pós-RC 37B: jornada transacional de compras empresariais.
create table if not exists sigov.compras_numeracao (
 tenant_id uuid not null, tipo varchar(30) not null, ano int not null, ultimo_numero bigint not null default 0,
 updated_at timestamptz not null default now(), primary key(tenant_id,tipo,ano));

create table if not exists sigov.compras_fornecedor (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, codigo varchar(30) not null,
 tipo_pessoa char(1) not null check(tipo_pessoa in('F','J')), documento_hash char(64) not null,
 documento_mascarado varchar(30) not null, razao_social varchar(200) not null, nome_fantasia varchar(200),
 categoria varchar(100), porte varchar(30), condicao_pagamento varchar(100), prazo_medio int not null default 0 check(prazo_medio>=0),
 observacoes text, score numeric(5,2) not null default 0 check(score between 0 and 100),
 status varchar(20) not null default 'RASCUNHO' check(status in('RASCUNHO','EM_ANALISE','ATIVO','SUSPENSO','BLOQUEADO','INATIVO')),
 created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false,
 unique(tenant_id,codigo), unique(tenant_id,documento_hash));
create index if not exists compras_fornecedor_busca_idx on sigov.compras_fornecedor(tenant_id,status,razao_social) where not is_deleted;

create table if not exists sigov.compras_fornecedor_contato (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, fornecedor_id uuid not null references sigov.compras_fornecedor(id), nome varchar(150) not null,
 email varchar(254), telefone varchar(30), principal boolean not null default false, created_at timestamptz not null default now(), created_by varchar(100) not null,
 updated_at timestamptz not null default now(), updated_by varchar(100) not null, correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false);
create table if not exists sigov.compras_fornecedor_endereco (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, fornecedor_id uuid not null references sigov.compras_fornecedor(id), tipo varchar(30) not null,
 logradouro varchar(200) not null, numero varchar(30), complemento varchar(100), bairro varchar(100), cidade varchar(100) not null, uf char(2) not null, cep varchar(10),
 created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false);
create table if not exists sigov.compras_fornecedor_documento (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, fornecedor_id uuid not null references sigov.compras_fornecedor(id), documento_ged_id uuid not null,
 tipo varchar(60) not null, obrigatorio boolean not null default false, validade date, status varchar(20) not null default 'VALIDO',
 created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false);
create index if not exists compras_documento_validade_idx on sigov.compras_fornecedor_documento(tenant_id,validade) where not is_deleted;

create table if not exists sigov.compras_requisicao (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, numero varchar(40) not null, solicitante_id uuid not null, setor varchar(100), centro_custo_id uuid,
 projeto_id uuid, contrato_id uuid, ordem_servico_id uuid, almoxarifado_id uuid, urgencia varchar(20) not null default 'NORMAL', data_necessaria date,
 justificativa text not null, observacoes text, valor_estimado numeric(18,2) not null default 0, status varchar(30) not null default 'RASCUNHO',
 created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false, unique(tenant_id,numero));
create table if not exists sigov.compras_requisicao_item (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, requisicao_id uuid not null references sigov.compras_requisicao(id), ordem int not null,
 tipo varchar(20) not null check(tipo in('MATERIAL','SERVICO','ATIVO','PECA_OS')), descricao varchar(500) not null, especificacao text, unidade varchar(20) not null,
 quantidade numeric(18,4) not null check(quantidade>0), valor_estimado numeric(18,4) not null check(valor_estimado>=0), permite_parcial boolean not null default true,
 exige_inspecao boolean not null default false, created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(),
 updated_by varchar(100) not null, correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false,
 unique(tenant_id,requisicao_id,ordem));

create table if not exists sigov.compras_aprovacao (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, requisicao_id uuid not null references sigov.compras_requisicao(id), nivel int not null,
 aprovador_id uuid not null, limite numeric(18,2) not null, status varchar(20) not null default 'PENDENTE', motivo text, regra_snapshot jsonb not null default '{}'::jsonb,
 decidido_em timestamptz, created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, unique(tenant_id,requisicao_id,nivel,aprovador_id));
create index if not exists compras_aprovacao_fila_idx on sigov.compras_aprovacao(tenant_id,aprovador_id,status,created_at);

create table if not exists sigov.compras_cotacao (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, requisicao_id uuid not null references sigov.compras_requisicao(id), numero varchar(40) not null,
 rodada int not null default 1, prazo timestamptz not null, status varchar(20) not null default 'RASCUNHO', created_at timestamptz not null default now(), created_by varchar(100) not null,
 updated_at timestamptz not null default now(), updated_by varchar(100) not null, correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false,
 unique(tenant_id,numero));
create table if not exists sigov.compras_cotacao_convite (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, cotacao_id uuid not null references sigov.compras_cotacao(id), fornecedor_id uuid not null references sigov.compras_fornecedor(id),
 token_hash char(64) not null unique, expira_em timestamptz not null, revogado_em timestamptz, acessado_em timestamptz, respondido_em timestamptz, entregue_em timestamptz,
 status varchar(20) not null default 'PENDENTE', created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(),
 updated_by varchar(100) not null, correlation_id varchar(100) not null, version bigint not null default 1, unique(tenant_id,cotacao_id,fornecedor_id));
create table if not exists sigov.compras_cotacao_resposta_item (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, convite_id uuid not null references sigov.compras_cotacao_convite(id), requisicao_item_id uuid not null references sigov.compras_requisicao_item(id),
 preco_unitario numeric(18,4) not null check(preco_unitario>=0), desconto numeric(18,2) not null default 0, imposto numeric(18,2) not null default 0, frete numeric(18,2) not null default 0,
 prazo_dias int not null default 0, marca varchar(100), fabricante varchar(100), recusado boolean not null default false, created_at timestamptz not null default now(), created_by varchar(100) not null,
 updated_at timestamptz not null default now(), updated_by varchar(100) not null, correlation_id varchar(100) not null, version bigint not null default 1, unique(tenant_id,convite_id,requisicao_item_id));

create table if not exists sigov.compras_pedido (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, numero varchar(40) not null, requisicao_id uuid not null references sigov.compras_requisicao(id), cotacao_id uuid references sigov.compras_cotacao(id),
 fornecedor_id uuid not null references sigov.compras_fornecedor(id), total numeric(18,2) not null check(total>=0), status varchar(30) not null default 'RASCUNHO', previsao date,
 created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, is_deleted boolean not null default false, unique(tenant_id,numero));
create table if not exists sigov.compras_recebimento (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, pedido_id uuid not null references sigov.compras_pedido(id), documento varchar(100) not null, idempotency_key varchar(200) not null,
 resultado_inspecao varchar(30) not null, confirmado_em timestamptz, created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(),
 updated_by varchar(100) not null, correlation_id varchar(100) not null, version bigint not null default 1, unique(tenant_id,idempotency_key));
create table if not exists sigov.compras_fatura (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, pedido_id uuid not null references sigov.compras_pedido(id), fornecedor_id uuid not null references sigov.compras_fornecedor(id),
 numero varchar(60) not null, serie varchar(20) not null default '', total numeric(18,2) not null check(total>=0), resultado_match varchar(40), status varchar(30) not null default 'EM_CONFERENCIA',
 conta_pagar_id uuid, created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, unique(tenant_id,fornecedor_id,numero,serie));
create table if not exists sigov.compras_devolucao (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, recebimento_id uuid not null references sigov.compras_recebimento(id), motivo text not null, idempotency_key varchar(200) not null,
 status varchar(20) not null default 'REGISTRADA', created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1, unique(tenant_id,idempotency_key));
create table if not exists sigov.compras_fornecedor_avaliacao (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, fornecedor_id uuid not null references sigov.compras_fornecedor(id), pedido_id uuid references sigov.compras_pedido(id),
 qualidade numeric(5,2) not null, prazo numeric(5,2) not null, preco numeric(5,2) not null, atendimento numeric(5,2) not null, nota numeric(5,2) not null,
 created_at timestamptz not null default now(), created_by varchar(100) not null, updated_at timestamptz not null default now(), updated_by varchar(100) not null,
 correlation_id varchar(100) not null, version bigint not null default 1);

create table if not exists sigov.compras_historico (
 id uuid primary key default gen_random_uuid(), tenant_id uuid not null, aggregate_type varchar(40) not null, aggregate_id uuid not null, acao varchar(60) not null,
 detalhes jsonb not null default '{}'::jsonb, created_at timestamptz not null default now(), created_by varchar(100) not null, correlation_id varchar(100) not null);
create index if not exists compras_historico_timeline_idx on sigov.compras_historico(tenant_id,aggregate_type,aggregate_id,created_at desc);
create table if not exists sigov.compras_idempotencia (
 tenant_id uuid not null, operacao varchar(60) not null, chave varchar(200) not null, recurso_id uuid not null, created_at timestamptz not null default now(), primary key(tenant_id,operacao,chave));

insert into sigov.modulo_saas(codigo,nome,descricao,categoria,ordem,rota_base,icone,ativo)
values('COMPRAS_EMPRESARIAIS','Compras Empresariais','Procure-to-pay empresarial integrado.','Empresarial',75,'/ComprasEmpresariais','cart',true)
on conflict(codigo) do update set nome=excluded.nome,descricao=excluded.descricao,rota_base=excluded.rota_base,ativo=true,is_deleted=false;

insert into sigov.permissao(modulo,chave,descricao,ativo)
select 'COMPRAS_EMPRESARIAIS', chave, descricao, true from (values
('compras_empresariais.dashboard.visualizar','Visualizar cockpit de compras'),('compras_empresariais.fornecedores.visualizar','Visualizar fornecedores'),('compras_empresariais.fornecedores.criar','Criar fornecedores'),('compras_empresariais.fornecedores.editar','Editar fornecedores'),('compras_empresariais.fornecedores.bloquear','Bloquear fornecedores'),('compras_empresariais.fornecedores.dados_bancarios','Gerenciar dados bancários'),('compras_empresariais.requisicoes.visualizar','Visualizar requisições'),('compras_empresariais.requisicoes.criar','Criar requisições'),('compras_empresariais.requisicoes.editar','Editar requisições'),('compras_empresariais.requisicoes.enviar','Enviar requisições'),('compras_empresariais.requisicoes.cancelar','Cancelar requisições'),('compras_empresariais.aprovacoes.visualizar','Visualizar aprovações'),('compras_empresariais.aprovacoes.aprovar','Decidir aprovações'),('compras_empresariais.aprovacoes.delegar','Delegar aprovações'),('compras_empresariais.cotacoes.visualizar','Visualizar cotações'),('compras_empresariais.cotacoes.criar','Criar cotações'),('compras_empresariais.cotacoes.enviar','Enviar cotações'),('compras_empresariais.cotacoes.julgar','Julgar cotações'),('compras_empresariais.pedidos.visualizar','Visualizar pedidos'),('compras_empresariais.pedidos.criar','Criar pedidos'),('compras_empresariais.pedidos.aprovar','Aprovar pedidos'),('compras_empresariais.pedidos.emitir','Emitir pedidos'),('compras_empresariais.pedidos.cancelar','Cancelar pedidos'),('compras_empresariais.recebimentos.visualizar','Visualizar recebimentos'),('compras_empresariais.recebimentos.registrar','Registrar recebimentos'),('compras_empresariais.recebimentos.inspecionar','Inspecionar recebimentos'),('compras_empresariais.faturas.visualizar','Visualizar faturas'),('compras_empresariais.faturas.conferir','Conferir faturas'),('compras_empresariais.faturas.aprovar','Aprovar faturas'),('compras_empresariais.devolucoes.visualizar','Visualizar devoluções'),('compras_empresariais.devolucoes.criar','Criar devoluções'),('compras_empresariais.avaliacoes.gerenciar','Gerenciar avaliações'),('compras_empresariais.relatorios.visualizar','Visualizar relatórios'),('compras_empresariais.configuracao.gerenciar','Gerenciar configurações')) p(chave,descricao)
on conflict(chave) do update set modulo=excluded.modulo,descricao=excluded.descricao,ativo=true,is_deleted=false;
