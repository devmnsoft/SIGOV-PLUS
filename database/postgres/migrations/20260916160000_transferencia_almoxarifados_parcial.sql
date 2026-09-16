-- Transferências de consumo entre almoxarifados: expedição, trânsito e recebimentos parciais.
alter table sigov.almoxarifado_transferencia drop constraint if exists ck_rc5089_transf_status;
alter table sigov.almoxarifado_transferencia
 add column if not exists responsavel varchar(200),
 add column if not exists idempotency_key varchar(100),
 add column if not exists expedida_em timestamptz,
 add column if not exists expedida_por bigint,
 add column if not exists recebida_em timestamptz;
update sigov.almoxarifado_transferencia set responsavel='Não informado (registro legado)',idempotency_key='legado-'||id where responsavel is null or idempotency_key is null;
alter table sigov.almoxarifado_transferencia alter column responsavel set not null,alter column idempotency_key set not null;
do $$ begin if not exists(select 1 from pg_constraint where conrelid='sigov.almoxarifado_transferencia'::regclass and conname='ck_almox_transferencia_status') then alter table sigov.almoxarifado_transferencia add constraint ck_almox_transferencia_status check(status in('RASCUNHO','PENDENTE','EM_TRANSITO','RECEBIDA_PARCIAL','DIVERGENCIA','RECEBIDA','CONFERIDA','CANCELADA')); end if; end $$;
create unique index if not exists ux_almox_transferencia_idempotencia on sigov.almoxarifado_transferencia(tenant_id,entidade_id,idempotency_key);

alter table sigov.almoxarifado_transferencia_item
 add column if not exists quantidade_recebida numeric(18,4) not null default 0,
 add column if not exists quantidade_recusada numeric(18,4) not null default 0,
 add column if not exists divergencia text,
 add column if not exists movimento_saida_id bigint references sigov.almoxarifado_movimentacao(id);
alter table sigov.almoxarifado_transferencia_item drop constraint if exists ck_rc5089_transf_item_qtd;
do $$ begin if not exists(select 1 from pg_constraint where conrelid='sigov.almoxarifado_transferencia_item'::regclass and conname='ck_almox_transferencia_item_qtd') then alter table sigov.almoxarifado_transferencia_item add constraint ck_almox_transferencia_item_qtd check(quantidade>0 and quantidade_recebida>=0 and quantidade_recusada>=0 and quantidade_recebida+quantidade_recusada<=quantidade); end if; end $$;

create table if not exists sigov.almoxarifado_transferencia_recebimento(
 id bigint generated always as identity primary key,tenant_id bigint not null,entidade_id bigint not null,
 transferencia_id bigint not null references sigov.almoxarifado_transferencia(id),item_id bigint not null references sigov.almoxarifado_transferencia_item(id),
 quantidade numeric(18,4) not null,movimento_entrada_id bigint not null references sigov.almoxarifado_movimentacao(id),
 idempotency_key varchar(100) not null,usuario_id bigint not null,correlation_id varchar(100) not null,ocorrido_em timestamptz not null default now(),
 constraint ck_almox_transferencia_recebimento_qtd check(quantidade>0));
create unique index if not exists ux_almox_transferencia_recebimento_mov on sigov.almoxarifado_transferencia_recebimento(movimento_entrada_id);

create table if not exists sigov.almoxarifado_transferencia_evento(
 id bigint generated always as identity primary key,tenant_id bigint not null,entidade_id bigint not null,
 transferencia_id bigint not null references sigov.almoxarifado_transferencia(id),tipo varchar(40) not null,dados jsonb not null default '{}'::jsonb,
 usuario_id bigint not null,correlation_id varchar(100) not null,idempotency_key varchar(100),ocorrido_em timestamptz not null default now());
create unique index if not exists ux_almox_transferencia_evento_idempotencia on sigov.almoxarifado_transferencia_evento(tenant_id,transferencia_id,idempotency_key) where idempotency_key is not null;
create index if not exists ix_almox_transferencia_evento_timeline on sigov.almoxarifado_transferencia_evento(tenant_id,entidade_id,transferencia_id,ocorrido_em,id);

insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'almoxarifado',v.chave,v.recurso,v.acao,v.descricao,true,false from(values
('almoxarifado.transferencia.visualizar','almoxarifado.transferencia','visualizar','Visualizar transferências'),
('almoxarifado.transferencia.criar','almoxarifado.transferencia','criar','Criar transferências'),
('almoxarifado.transferencia.expedir','almoxarifado.transferencia','expedir','Expedir transferências'),
('almoxarifado.transferencia.receber','almoxarifado.transferencia','receber','Receber transferências'))v(chave,recurso,acao,descricao)
where not exists(select 1 from sigov.permissao p where p.chave=v.chave);
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false from sigov.perfil_acesso pa join sigov.permissao p on p.modulo='almoxarifado' and p.chave like 'almoxarifado.transferencia.%'
where pa.codigo_externo='SUPERADMIN' and pa.sistemico and pa.ativo and not pa.is_deleted
on conflict(perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;
