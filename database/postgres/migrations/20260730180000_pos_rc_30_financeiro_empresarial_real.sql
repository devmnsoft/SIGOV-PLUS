-- Pós-RC 30: evolução incremental do Financeiro Empresarial (não altera o SIAFIC).
do $$
declare t text;
begin
  foreach t in array array['financeiro_conta_receber','financeiro_conta_pagar','financeiro_conta_bancaria','financeiro_movimento'] loop
    execute format('alter table sigov.%I add column if not exists version bigint not null default 1',t);
    execute format('alter table sigov.%I add column if not exists is_deleted boolean not null default false',t);
    execute format('alter table sigov.%I add column if not exists deleted_at timestamptz null',t);
    execute format('alter table sigov.%I add column if not exists deleted_by bigint null',t);
    execute format('alter table sigov.%I add column if not exists created_by bigint null',t);
    execute format('alter table sigov.%I add column if not exists updated_by bigint null',t);
    execute format('alter table sigov.%I add column if not exists correlation_id uuid null',t);
    execute format('alter table sigov.%I add column if not exists idempotency_key varchar(160) null',t);
  end loop;
end $$;

alter table sigov.financeiro_conta_receber add column if not exists documento_referencia varchar(160), add column if not exists pedido_comercial_id uuid, add column if not exists cliente_enterprise_id uuid;
alter table sigov.financeiro_conta_pagar add column if not exists documento_referencia varchar(160), add column if not exists fornecedor_enterprise_id uuid, add column if not exists aprovado_em timestamptz, add column if not exists aprovado_por bigint;
alter table sigov.financeiro_movimento add column if not exists movimento_original_id bigint;
alter table sigov.financeiro_baixa_receber add column if not exists idempotency_key varchar(160), add column if not exists estornado boolean not null default false, add column if not exists estornado_em timestamptz, add column if not exists estornado_por bigint, add column if not exists estorno_motivo text, add column if not exists movimento_estorno_id bigint;
alter table sigov.financeiro_baixa_pagar add column if not exists idempotency_key varchar(160), add column if not exists estornado boolean not null default false, add column if not exists estornado_em timestamptz, add column if not exists estornado_por bigint, add column if not exists estorno_motivo text, add column if not exists movimento_estorno_id bigint;

create unique index if not exists ux_fin_cr_tenant_idempotency on sigov.financeiro_conta_receber(tenant_id,idempotency_key) where idempotency_key is not null and not is_deleted;
create unique index if not exists ux_fin_cp_tenant_idempotency on sigov.financeiro_conta_pagar(tenant_id,idempotency_key) where idempotency_key is not null and not is_deleted;
create unique index if not exists ux_fin_mov_tenant_idempotency on sigov.financeiro_movimento(tenant_id,idempotency_key) where idempotency_key is not null and not is_deleted;
create unique index if not exists ux_fin_br_tenant_idempotency on sigov.financeiro_baixa_receber(tenant_id,idempotency_key) where idempotency_key is not null;
create unique index if not exists ux_fin_bp_tenant_idempotency on sigov.financeiro_baixa_pagar(tenant_id,idempotency_key) where idempotency_key is not null;
create unique index if not exists ux_fin_cr_pedido_parcela on sigov.financeiro_conta_receber(tenant_id,pedido_comercial_id,parcela) where pedido_comercial_id is not null and not is_deleted;
create index if not exists ix_fin_cr_tenant_competencia on sigov.financeiro_conta_receber(tenant_id,competencia);
create index if not exists ix_fin_cp_tenant_competencia on sigov.financeiro_conta_pagar(tenant_id,competencia);
create index if not exists ix_fin_mov_tenant_conta_data on sigov.financeiro_movimento(tenant_id,conta_bancaria_id,data_movimento);

alter table sigov.financeiro_conta_receber drop constraint if exists ck_fin_cr_valores;
alter table sigov.financeiro_conta_receber add constraint ck_fin_cr_valores check(valor_original>0 and valor_aberto>=0 and valor_aberto<=valor_original+valor_acrescimo and valor_desconto>=0 and valor_acrescimo>=0);
alter table sigov.financeiro_conta_pagar drop constraint if exists ck_fin_cp_valores;
alter table sigov.financeiro_conta_pagar add constraint ck_fin_cp_valores check(valor_original>0 and valor_aberto>=0 and valor_aberto<=valor_original+valor_acrescimo and valor_desconto>=0 and valor_acrescimo>=0);

create table if not exists sigov.financeiro_caixa(id bigserial primary key,tenant_id bigint not null,nome varchar(160) not null,saldo_atual numeric(14,2) not null default 0,ativo boolean not null default true,version bigint not null default 1,is_deleted boolean not null default false,created_at timestamptz not null default now(),unique(tenant_id,nome));
create table if not exists sigov.financeiro_caixa_sessao(id bigserial primary key,tenant_id bigint not null,caixa_id bigint not null,status varchar(30) not null,saldo_abertura numeric(14,2) not null,saldo_fechamento numeric(14,2),aberto_em timestamptz not null default now(),aberto_por bigint,fechado_em timestamptz,fechado_por bigint,version bigint not null default 1);
create unique index if not exists ux_fin_caixa_sessao_aberta on sigov.financeiro_caixa_sessao(tenant_id,caixa_id) where status='ABERTA';
create table if not exists sigov.financeiro_transferencia(id bigserial primary key,tenant_id bigint not null,conta_origem_id bigint not null,conta_destino_id bigint not null,valor numeric(14,2) not null,descricao varchar(300) not null,status varchar(30) not null,movimento_saida_id bigint,movimento_entrada_id bigint,idempotency_key varchar(160) not null,correlation_id uuid not null,created_by bigint,created_at timestamptz not null default now(),version bigint not null default 1,check(valor>0),check(conta_origem_id<>conta_destino_id),unique(tenant_id,idempotency_key));
create table if not exists sigov.financeiro_titulo_historico(id bigserial primary key,tenant_id bigint not null,tipo_titulo varchar(40) not null,titulo_id bigint not null,acao varchar(120) not null,antes jsonb,depois jsonb,usuario_id bigint,correlation_id uuid not null,created_at timestamptz not null default now());
create index if not exists ix_fin_hist_tenant_titulo on sigov.financeiro_titulo_historico(tenant_id,tipo_titulo,titulo_id,created_at);
create table if not exists sigov.financeiro_integracao_origem(id bigserial primary key,tenant_id bigint not null,enterprise_tenant_id uuid,evento_id uuid,origem varchar(80) not null,origem_uuid uuid,status varchar(30) not null,erro text,idempotency_key varchar(160) not null,created_at timestamptz not null default now(),processed_at timestamptz,unique(tenant_id,idempotency_key));
create index if not exists ix_fin_integracao_tenant_status on sigov.financeiro_integracao_origem(tenant_id,status,created_at);

insert into sigov.permissao(modulo,recurso,acao,chave,descricao,ativo) values
('financeiro_empresarial','dashboard','visualizar','financeiro_empresarial.dashboard.visualizar','Visualizar dashboard empresarial',true),
('financeiro_empresarial','contas_receber','baixar','financeiro_empresarial.contas_receber.baixar','Baixar contas a receber',true),
('financeiro_empresarial','contas_receber','estornar','financeiro_empresarial.contas_receber.estornar','Estornar contas a receber',true),
('financeiro_empresarial','contas_pagar','pagar','financeiro_empresarial.contas_pagar.pagar','Pagar contas a pagar',true),
('financeiro_empresarial','contas_pagar','estornar','financeiro_empresarial.contas_pagar.estornar','Estornar contas a pagar',true),
('financeiro_empresarial','transferencias','criar','financeiro_empresarial.transferencias.criar','Transferir valores',true),
('financeiro_empresarial','fluxo_caixa','visualizar','financeiro_empresarial.fluxo_caixa.visualizar','Visualizar fluxo de caixa',true)
on conflict(modulo,chave) do update set descricao=excluded.descricao,ativo=true,is_deleted=false;
