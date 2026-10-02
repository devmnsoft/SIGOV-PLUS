select id, modulo, recurso, acao, ativo, is_deleted from sigov.permissao where modulo in ('compras_empresariais','COMPRAS_EMPRESARIAIS','compras') order by modulo,recurso,acao;
select pp.id, pp.perfil_acesso_id, p.modulo, p.recurso, p.acao, pp.efeito, pp.alcada_valor, pp.tenant_id, pp.entidade_id, pp.exercicio_id, pp.unidade_id
from sigov.perfil_permissao pp join sigov.permissao p on p.id=pp.permissao_id
where p.modulo in ('compras_empresariais','COMPRAS_EMPRESARIAIS','compras') order by pp.perfil_acesso_id, p.recurso;
select id, tenant_id, entidade_id, exercicio_id, login, ativo, nome from sigov.usuario where tenant_id in (1,5) or id in (101,102) order by id limit 40;
select ga.id grupo_acesso, gp.grupo_perfil_id, pa.id perfil_acesso, pa.nome perfil_nome, ug.usuario_id from sigov.usuario_grupo ug join sigov.grupo_acesso ga on ga.id=ug.grupo_acesso_id left join sigov.grupo_perfil gp on gp.grupo_acesso_id=ga.id left join sigov.perfil_acesso pa on pa.id=gp.perfil_acesso_id where ug.usuario_id in (101,102,1) order by ug.usuario_id, ga.id;
