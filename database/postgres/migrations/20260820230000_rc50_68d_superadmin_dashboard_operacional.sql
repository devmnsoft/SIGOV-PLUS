-- RC50.68D: o dashboard é read-only; esta migration provisiona somente a autoridade persistente.
insert into sigov.permissao(modulo,chave,recurso,acao,descricao,ativo,is_deleted)
select 'saas',v.chave,'saas.superadmin.dashboard',v.acao,v.descricao,true,false
from (values
 ('saas.superadmin.dashboard.visualizar','visualizar','Visualizar dashboard operacional SuperAdmin'),
 ('saas.superadmin.dashboard.exportar','exportar','Exportar dados operacionais protegidos')) v(chave,acao,descricao)
where not exists(select 1 from sigov.permissao p where p.modulo='saas' and p.chave=v.chave);

insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id,efeito,ativo,is_deleted)
select pa.id,p.id,'PERMITIR',true,false
from sigov.perfil_acesso pa cross join sigov.permissao p
where pa.codigo_externo='SUPERADMIN' and pa.sistemico and pa.ativo and not pa.is_deleted
  and p.recurso='saas.superadmin.dashboard' and p.ativo and not p.is_deleted
on conflict(perfil_acesso_id,permissao_id) do update set efeito='PERMITIR',ativo=true,is_deleted=false;
