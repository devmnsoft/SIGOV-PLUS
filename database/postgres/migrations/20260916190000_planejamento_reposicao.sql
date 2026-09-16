-- Política de reposição por material e almoxarifado, com vigência e concorrência otimista.
create table if not exists sigov.almoxarifado_politica_reposicao(
 id bigint generated always as identity primary key,
 tenant_id bigint not null,
 entidade_id bigint not null,
 almoxarifado_id bigint not null references sigov.almoxarifado_local(id),
 material_id bigint not null references sigov.almoxarifado_material(id),
 estoque_minimo numeric(18,4) not null,
 estoque_alvo numeric(18,4) not null,
 multiplo_compra numeric(18,4),
 quantidade_minima_pedido numeric(18,4),
 prazo_reposicao_dias integer,
 fornecedor_preferencial_id bigint references sigov.compras_fornecedor(id),
 vigencia_inicio date not null,
 vigencia_fim date,
 ativa boolean not null default true,
 versao bigint not null default 1,
 created_at timestamptz not null default now(),
 updated_at timestamptz not null default now(),
 created_by bigint,
 updated_by bigint,
 constraint ux_almox_politica_reposicao unique(tenant_id,entidade_id,almoxarifado_id,material_id),
 constraint ck_almox_politica_reposicao_quantidades check(estoque_minimo>=0 and estoque_alvo>=estoque_minimo and (multiplo_compra is null or multiplo_compra>0) and (quantidade_minima_pedido is null or quantidade_minima_pedido>=0)),
 constraint ck_almox_politica_reposicao_prazo check(prazo_reposicao_dias is null or prazo_reposicao_dias>=0),
 constraint ck_almox_politica_reposicao_vigencia check(vigencia_fim is null or vigencia_fim>=vigencia_inicio)
);
create index if not exists ix_almox_politica_reposicao_painel on sigov.almoxarifado_politica_reposicao(tenant_id,entidade_id,almoxarifado_id,ativa);
insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'almoxarifado',chave,recurso,acao,descricao,true,false from (values
 ('almoxarifado.reposicao.visualizar','almoxarifado.reposicao','visualizar','Visualizar planejamento de reposição'),
 ('almoxarifado.reposicao.configurar','almoxarifado.reposicao','configurar','Configurar políticas de reposição')) p(chave,recurso,acao,descricao)
on conflict(chave) do update set modulo=excluded.modulo,recurso=excluded.recurso,acao=excluded.acao,descricao=excluded.descricao,ativo=true,is_deleted=false;
insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.codigo_externo='SUPERADMIN' and pa.sistemico and pa.ativo and not pa.is_deleted and p.chave in('almoxarifado.reposicao.visualizar','almoxarifado.reposicao.configurar')
on conflict(perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;
