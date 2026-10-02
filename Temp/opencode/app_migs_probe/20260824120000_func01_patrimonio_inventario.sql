-- FUNC01: Patrimônio, Inventário e Responsabilidade Patrimonial.
-- Preserva contratos UUID legados por renomeação não destrutiva antes do contrato bigint.
do $$
declare t text;
begin
  foreach t in array array['patrimonio_bem','patrimonio_inventario','patrimonio_inventario_item','patrimonio_baixa'] loop
    if exists(select 1 from information_schema.columns where table_schema='sigov' and table_name=t and column_name='id' and data_type='uuid') then
      if to_regclass('sigov.'||t||'_legado_uuid') is null then execute format('alter table sigov.%I rename to %I',t,t||'_legado_uuid');
      else raise exception 'FUNC01 não pode preservar %.%: destino legado já existe', 'sigov',t; end if;
    end if;
  end loop;
end $$;

create table if not exists sigov.patrimonio_categoria (
 id bigint generated always as identity primary key, tenant_id bigint not null, codigo varchar(40) not null,
 nome varchar(160) not null, descricao text, vida_util_meses integer, ativo boolean not null default true,
 is_deleted boolean not null default false, created_at timestamptz not null default now(), created_by bigint,
 updated_at timestamptz not null default now(), updated_by bigint, deleted_at timestamptz, deleted_by bigint,
 constraint ck_patrimonio_categoria_vida check(vida_util_meses is null or vida_util_meses>0),
 constraint ux_patrimonio_categoria_tenant_codigo unique(tenant_id,codigo));

create table if not exists sigov.patrimonio_bem (
 id bigint generated always as identity primary key, tenant_id bigint not null, codigo_tombo varchar(80) not null,
 codigo_anterior varchar(80), descricao varchar(500) not null, categoria_id bigint, tipo_bem varchar(80) not null,
 marca varchar(120), modelo varchar(120), numero_serie varchar(160), data_aquisicao date,
 valor_aquisicao numeric(18,2), valor_atual numeric(18,2), estado_conservacao varchar(30) not null,
 situacao varchar(30) not null default 'ATIVO', unidade_id bigint, setor_id bigint, responsavel_usuario_id bigint,
 localizacao varchar(300), observacao text, ativo boolean not null default true, is_deleted boolean not null default false,
 created_at timestamptz not null default now(), created_by bigint, updated_at timestamptz not null default now(),
 updated_by bigint, deleted_at timestamptz, deleted_by bigint,
 constraint fk_patrimonio_bem_categoria foreign key(categoria_id) references sigov.patrimonio_categoria(id),
 constraint ux_patrimonio_bem_tenant_tombo unique(tenant_id,codigo_tombo),
 constraint ck_patrimonio_bem_valores check((valor_aquisicao is null or valor_aquisicao>=0) and (valor_atual is null or valor_atual>=0)),
 constraint ck_patrimonio_bem_situacao check(situacao in ('ATIVO','EM_MANUTENCAO','CEDIDO','NAO_LOCALIZADO','BAIXADO')));

create table if not exists sigov.patrimonio_movimentacao (
 id bigint generated always as identity primary key, tenant_id bigint not null, bem_id bigint not null,
 unidade_origem_id bigint, unidade_destino_id bigint, responsavel_origem_id bigint, responsavel_destino_id bigint,
 localizacao_origem varchar(300), localizacao_destino varchar(300), tipo_movimentacao varchar(50) not null,
 justificativa text not null, data_movimentacao timestamptz not null, usuario_id bigint, correlation_id varchar(100) not null,
 created_at timestamptz not null default now(), constraint fk_patrimonio_mov_bem foreign key(bem_id) references sigov.patrimonio_bem(id));

create table if not exists sigov.patrimonio_inventario (
 id bigint generated always as identity primary key, tenant_id bigint not null, codigo varchar(80) not null,
 descricao varchar(500) not null, data_abertura date not null, data_fechamento date, situacao varchar(20) not null default 'ABERTO',
 unidade_id bigint, responsavel_usuario_id bigint, created_at timestamptz not null default now(), created_by bigint,
 updated_at timestamptz not null default now(), updated_by bigint,
 constraint ux_patrimonio_inventario_tenant_codigo unique(tenant_id,codigo),
 constraint ck_patrimonio_inventario_situacao check(situacao in ('ABERTO','FECHADO','CANCELADO')),
 constraint ck_patrimonio_inventario_datas check(data_fechamento is null or data_fechamento>=data_abertura));

create table if not exists sigov.patrimonio_inventario_item (
 id bigint generated always as identity primary key, tenant_id bigint not null, inventario_id bigint not null, bem_id bigint not null,
 localizado boolean, estado_informado varchar(30), localizacao_informada varchar(300), divergencia boolean not null default false,
 descricao_divergencia text, conferido_por_usuario_id bigint, conferido_em timestamptz, observacao text,
 constraint fk_patrimonio_inv_item_inv foreign key(inventario_id) references sigov.patrimonio_inventario(id) on delete cascade,
 constraint fk_patrimonio_inv_item_bem foreign key(bem_id) references sigov.patrimonio_bem(id),
 constraint ux_patrimonio_inv_item unique(inventario_id,bem_id));

create table if not exists sigov.patrimonio_baixa (
 id bigint generated always as identity primary key, tenant_id bigint not null, bem_id bigint not null,
 tipo_baixa varchar(50) not null, justificativa text not null, data_baixa date not null, valor_baixa numeric(18,2),
 autorizado_por_usuario_id bigint, created_at timestamptz not null default now(),
 constraint fk_patrimonio_baixa_bem foreign key(bem_id) references sigov.patrimonio_bem(id),
 constraint ux_patrimonio_baixa_bem unique(tenant_id,bem_id), constraint ck_patrimonio_baixa_valor check(valor_baixa is null or valor_baixa>=0));

create table if not exists sigov.patrimonio_auditoria (
 id bigint generated always as identity primary key, tenant_id bigint not null, entidade varchar(100) not null,
 entidade_id bigint not null, operacao varchar(50) not null, antes jsonb, depois jsonb, usuario_id bigint,
 correlation_id varchar(100) not null, ocorrido_em_utc timestamptz not null default now());

create index if not exists ix_patrimonio_categoria_tenant on sigov.patrimonio_categoria(tenant_id) where not is_deleted;
create index if not exists ix_func01_patrimonio_bem_tenant on sigov.patrimonio_bem(tenant_id) where not is_deleted;
create index if not exists ix_patrimonio_bem_tombo on sigov.patrimonio_bem(tenant_id,codigo_tombo);
create index if not exists ix_patrimonio_bem_situacao on sigov.patrimonio_bem(tenant_id,situacao) where not is_deleted;
create index if not exists ix_patrimonio_bem_unidade on sigov.patrimonio_bem(tenant_id,unidade_id) where not is_deleted;
create index if not exists ix_patrimonio_bem_responsavel on sigov.patrimonio_bem(tenant_id,responsavel_usuario_id) where not is_deleted;
create index if not exists ix_patrimonio_mov_tenant_bem on sigov.patrimonio_movimentacao(tenant_id,bem_id,data_movimentacao desc);
create index if not exists ix_patrimonio_inv_tenant_situacao on sigov.patrimonio_inventario(tenant_id,situacao);
create index if not exists ix_patrimonio_inv_unidade on sigov.patrimonio_inventario(tenant_id,unidade_id);
create index if not exists ix_patrimonio_inv_responsavel on sigov.patrimonio_inventario(tenant_id,responsavel_usuario_id);
create index if not exists ix_patrimonio_inv_item_tenant on sigov.patrimonio_inventario_item(tenant_id,inventario_id);
create index if not exists ix_patrimonio_inv_divergencia on sigov.patrimonio_inventario_item(tenant_id,divergencia) where divergencia;
create index if not exists ix_patrimonio_baixa_tenant_data on sigov.patrimonio_baixa(tenant_id,data_baixa desc);
create index if not exists ix_patrimonio_auditoria_tenant_entidade on sigov.patrimonio_auditoria(tenant_id,entidade,entidade_id,ocorrido_em_utc desc);

-- FKs auxiliares só são adicionadas quando tabela, coluna e tipo bigint são compatíveis.
do $$
declare r record; cname text;
begin
 for r in select * from (values ('patrimonio_bem','unidade_id','unidade'),('patrimonio_bem','responsavel_usuario_id','usuario'),('patrimonio_inventario','unidade_id','unidade'),('patrimonio_inventario','responsavel_usuario_id','usuario')) v(src,col,dst) loop
  cname:='fk_'||r.src||'_'||r.col;
  if to_regclass('sigov.'||r.dst) is not null and exists(select 1 from information_schema.columns where table_schema='sigov' and table_name=r.dst and column_name='id' and data_type='bigint') and not exists(select 1 from pg_constraint where conname=cname) then
   execute format('alter table sigov.%I add constraint %I foreign key(%I) references sigov.%I(id) not valid',r.src,cname,r.col,r.dst);
  end if;
 end loop;
end $$;

insert into sigov.patrimonio_categoria(tenant_id,codigo,nome,descricao,vida_util_meses)
select t.id,v.codigo,v.nome,v.descricao,v.vida from sigov.tenant t cross join (values
 ('MOB','Mobiliário','Mesas, cadeiras, armários e similares',120),('INF','Equipamentos de informática','Computadores, monitores e periféricos',60),
 ('VEI','Veículos','Veículos e máquinas automotoras',120),('IMO','Imóveis','Edificações e terrenos',null::integer)) v(codigo,nome,descricao,vida)
where t.ativo and not t.is_deleted on conflict(tenant_id,codigo) do update set nome=excluded.nome,descricao=excluded.descricao,vida_util_meses=excluded.vida_util_meses,ativo=true,is_deleted=false;

insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'patrimonio',v.chave,v.recurso,v.acao,v.descricao,true,false from (values
 ('patrimonio.bem.visualizar','patrimonio.bem','visualizar','Visualizar bens patrimoniais'),('patrimonio.bem.criar','patrimonio.bem','criar','Cadastrar e tombar bens'),
 ('patrimonio.bem.editar','patrimonio.bem','editar','Editar bens'),('patrimonio.bem.movimentar','patrimonio.bem','movimentar','Movimentar bens'),
 ('patrimonio.bem.baixar','patrimonio.bem','baixar','Baixar bens'),('patrimonio.inventario.visualizar','patrimonio.inventario','visualizar','Visualizar inventários'),
 ('patrimonio.inventario.criar','patrimonio.inventario','criar','Abrir e fechar inventários'),('patrimonio.inventario.conferir','patrimonio.inventario','conferir','Conferir inventário'),
 ('patrimonio.dashboard.visualizar','patrimonio.dashboard','visualizar','Visualizar dashboard patrimonial'),('patrimonio.exportar','patrimonio','exportar','Exportar listagens patrimoniais')) v(chave,recurso,acao,descricao)
where not exists(select 1 from sigov.permissao p where p.modulo='patrimonio' and p.chave=v.chave);

insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.codigo_externo='SUPERADMIN' and pa.sistemico and pa.ativo and not pa.is_deleted and p.modulo='patrimonio' and p.ativo and not p.is_deleted
on conflict(perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;
