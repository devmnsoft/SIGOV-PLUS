-- Jornada patrimonial: recebimento aceito -> incorporação -> responsabilidade -> transferência/aceite.
-- A origem canônica permanece em Compras/Almoxarifado e o bem individual permanece patrimonio_bem.
alter table sigov.patrimonio_bem
 add column if not exists recebimento_item_id bigint,
 add column if not exists incorporacao_id bigint,
 add column if not exists identificacao_serial_origem varchar(160),
 add column if not exists disponibilidade_operacional varchar(24) not null default 'DISPONIVEL',
 add column if not exists versao bigint not null default 1;

do $$ begin
 if not exists(select 1 from pg_constraint where conname='fk_patrimonio_bem_recebimento_item') then
  alter table sigov.patrimonio_bem add constraint fk_patrimonio_bem_recebimento_item
   foreign key(recebimento_item_id) references sigov.compras_recebimento_item(id) not valid;
 end if;
 if not exists(select 1 from pg_constraint where conname='ck_patrimonio_bem_disponibilidade') then
  alter table sigov.patrimonio_bem add constraint ck_patrimonio_bem_disponibilidade
   check(disponibilidade_operacional in('DISPONIVEL','RESTRITO','INDISPONIVEL')) not valid;
 end if;
end $$;

create table if not exists sigov.patrimonio_incorporacao (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null,
 recebimento_item_id bigint not null references sigov.compras_recebimento_item(id), quantidade integer not null,
 correlation_id varchar(100) not null, usuario_id bigint not null, incorporado_em timestamptz not null default now(),
 constraint ck_patrimonio_incorporacao_quantidade check(quantidade>0),
 constraint ux_patrimonio_incorporacao_idempotencia unique(tenant_id,correlation_id));

do $$ begin
 if not exists(select 1 from pg_constraint where conname='fk_patrimonio_bem_incorporacao') then
  alter table sigov.patrimonio_bem add constraint fk_patrimonio_bem_incorporacao
   foreign key(incorporacao_id) references sigov.patrimonio_incorporacao(id) not valid;
 end if;
end $$;

create table if not exists sigov.patrimonio_termo_responsabilidade (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null,
 bem_id bigint not null references sigov.patrimonio_bem(id), responsavel_proposto_id bigint not null,
 responsavel_anterior_id bigint, unidade_id bigint, status varchar(20) not null default 'PENDENTE',
 conteudo_snapshot jsonb not null, motivo_recusa text, proposto_em timestamptz not null default now(),
 decidido_em timestamptz, vigencia_inicio timestamptz, vigencia_fim timestamptz, proposto_por bigint not null,
 decidido_por bigint, correlation_id varchar(100) not null,
 constraint ck_patrimonio_termo_status check(status in('PENDENTE','ACEITO','RECUSADO','CANCELADO')),
 constraint ck_patrimonio_termo_recusa check(status<>'RECUSADO' or nullif(trim(motivo_recusa),'') is not null),
 constraint ux_patrimonio_termo_idempotencia unique(tenant_id,correlation_id));

create unique index if not exists ux_patrimonio_termo_pendente
 on sigov.patrimonio_termo_responsabilidade(tenant_id,bem_id) where status='PENDENTE';

create table if not exists sigov.patrimonio_transferencia (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade_id bigint not null,
 bem_id bigint not null references sigov.patrimonio_bem(id), unidade_origem_id bigint, unidade_destino_id bigint not null,
 localizacao_origem varchar(300), localizacao_destino varchar(300), responsavel_origem_id bigint,
 responsavel_destino_id bigint, status varchar(20) not null default 'SOLICITADA', justificativa text not null,
 motivo_recusa text, solicitada_em timestamptz not null default now(), autorizada_em timestamptz,
 expedida_em timestamptz, recebida_em timestamptz, cancelada_em timestamptz, solicitada_por bigint not null,
 ultima_acao_por bigint not null, correlation_id varchar(100) not null, versao bigint not null default 1,
 constraint ck_patrimonio_transferencia_status check(status in('SOLICITADA','AUTORIZADA','EM_TRANSITO','CONCLUIDA','RECUSADA','CANCELADA')),
 constraint ck_patrimonio_transferencia_destino check(unidade_destino_id is distinct from unidade_origem_id),
 constraint ck_patrimonio_transferencia_justificativa check(nullif(trim(justificativa),'') is not null),
 constraint ux_patrimonio_transferencia_idempotencia unique(tenant_id,correlation_id));

create unique index if not exists ux_patrimonio_transferencia_aberta
 on sigov.patrimonio_transferencia(tenant_id,bem_id)
 where status in('SOLICITADA','AUTORIZADA','EM_TRANSITO');
create index if not exists ix_patrimonio_bem_origem on sigov.patrimonio_bem(tenant_id,recebimento_item_id);
create index if not exists ix_patrimonio_termo_historico on sigov.patrimonio_termo_responsabilidade(tenant_id,bem_id,proposto_em desc);
create index if not exists ix_patrimonio_transferencia_historico on sigov.patrimonio_transferencia(tenant_id,bem_id,solicitada_em desc);

insert into sigov.permissao(chave,descricao,modulo,recurso,acao,ativo,is_deleted)
select v.chave,v.nome,'patrimonio',v.recurso,v.acao,true,false from(values
 ('patrimonio.incorporacao.visualizar','Visualizar incorporações','incorporacao','visualizar'),
 ('patrimonio.incorporacao.executar','Executar incorporações','incorporacao','executar'),
 ('patrimonio.responsabilidade.propor','Propor responsabilidade','responsabilidade','propor'),
 ('patrimonio.responsabilidade.aceitar','Aceitar ou recusar responsabilidade','responsabilidade','aceitar'),
 ('patrimonio.movimentacao.operar','Operar etapas da movimentação','movimentacao','operar'))v(chave,nome,recurso,acao)
where not exists(select 1 from sigov.permissao p where p.chave=v.chave);

insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.codigo_externo='SUPERADMIN' and pa.sistemico and pa.ativo and not pa.is_deleted
 and p.modulo='patrimonio' and p.chave in('patrimonio.incorporacao.visualizar','patrimonio.incorporacao.executar','patrimonio.responsabilidade.propor','patrimonio.responsabilidade.aceitar','patrimonio.movimentacao.operar')
on conflict(perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;
