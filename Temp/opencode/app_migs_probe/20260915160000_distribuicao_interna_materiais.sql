-- Distribuição interna: reserva, expedição, recebimento e divergência rastreáveis.
create table if not exists sigov.almoxarifado_entrega(
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null,
 requisicao_id bigint not null references sigov.almoxarifado_requisicao(id), almoxarifado_id bigint not null references sigov.almoxarifado_local(id),
 status varchar(24) not null default 'SEPARADA', idempotency_key varchar(100) not null, observacao text,
 created_at timestamptz not null default now(), created_by bigint not null, expedida_em timestamptz, expedida_por bigint, recebida_em timestamptz, recebida_por bigint,
 constraint ck_almox_entrega_status check(status in('SEPARADA','EXPEDIDA','RECEBIDA_PARCIAL','RECEBIDA','DIVERGENCIA','CANCELADA')),
 constraint ux_almox_entrega_idempotencia unique(tenant_id,entidade_id,idempotency_key));
create table if not exists sigov.almoxarifado_entrega_item(
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null,
 entrega_id bigint not null references sigov.almoxarifado_entrega(id), requisicao_item_id bigint not null references sigov.almoxarifado_requisicao_item(id),
 quantidade_separada numeric(18,4) not null, quantidade_recebida numeric(18,4) not null default 0, quantidade_recusada numeric(18,4) not null default 0, lote varchar(100), divergencia text,
 constraint ck_almox_entrega_item_qtd check(quantidade_separada>0 and quantidade_recebida>=0 and quantidade_recusada>=0 and quantidade_recebida+quantidade_recusada<=quantidade_separada),
 constraint ux_almox_entrega_item unique(entrega_id,requisicao_item_id,lote));
create table if not exists sigov.almoxarifado_entrega_evento(
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null, entrega_id bigint not null references sigov.almoxarifado_entrega(id),
 tipo varchar(30) not null, dados jsonb, usuario_id bigint not null, correlation_id varchar(100) not null, ocorrido_em timestamptz not null default now());
create index if not exists ix_almox_entrega_fila on sigov.almoxarifado_entrega(tenant_id,entidade_id,status,created_at,id);
create index if not exists ix_almox_entrega_req on sigov.almoxarifado_entrega(tenant_id,entidade_id,requisicao_id,id);
insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'almoxarifado','almoxarifado.recebimento.confirmar','almoxarifado.recebimento','confirmar','Confirmar recebimento e divergências',true,false
where not exists(select 1 from sigov.permissao where chave='almoxarifado.recebimento.confirmar');
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false from sigov.perfil_acesso pa join sigov.permissao p on p.chave='almoxarifado.recebimento.confirmar'
where pa.codigo_externo='SUPERADMIN' and pa.sistemico and pa.ativo and not pa.is_deleted
on conflict(perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;
