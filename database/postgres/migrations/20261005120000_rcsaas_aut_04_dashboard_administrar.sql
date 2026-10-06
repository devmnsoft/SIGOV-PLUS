-- RC-SAAS-AUT Etapa B - Corretiva: chave ausente 'saas.superadmin.dashboard/administrar'.
-- O SaasAdminController (Web) avalia ('saas','saas.superadmin.dashboard','administrar') nas ações
-- de administração (contratos, usuários do cliente e bloqueios), mas o catálogo só continha
-- visualizar/exportar para esse recurso; após a convergência fail-closed do avaliador canônico,
-- as ações administrativas passaram a NEGAR para todos os perfis.
-- Esta migration cria a chave (idempotente) e espelha no grant os perfis que já administram a
-- plataforma via 'saas.plataforma.administrar' (1768), mantendo o banco como fonte de autoridade.

insert into sigov.permissao (modulo, recurso, chave, descricao, acao, critica, delegavel, ativo, is_deleted, created_at, correlation_id)
select 'saas', 'saas.superadmin.dashboard', 'saas.superadmin.dashboard.administrar',
       'Administração operacional do dashboard SaaS MNSOFT: clientes, usuários do cliente, contratos e bloqueios comerciais',
       'administrar', false, false, true, false, now(), gen_random_uuid()
where not exists (select 1 from sigov.permissao where modulo = 'saas' and chave = 'saas.superadmin.dashboard.administrar');

insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, tenant_id, efeito, ativo, is_deleted, created_at)
select pp.perfil_acesso_id, n.id, pp.tenant_id, 'PERMITIR', true, false, now()
from sigov.perfil_permissao pp
join sigov.perfil_acesso pa on pa.id = pp.perfil_acesso_id and pa.ativo and not pa.is_deleted
join sigov.permissao p on p.id = pp.permissao_id
join sigov.permissao n on n.modulo = 'saas' and n.chave = 'saas.superadmin.dashboard.administrar'
where p.modulo = 'saas' and p.chave = 'saas.plataforma.administrar'
  and pp.ativo and not pp.is_deleted and upper(pp.efeito) = 'PERMITIR'
  and not exists (select 1 from sigov.perfil_permissao x where x.perfil_acesso_id = pp.perfil_acesso_id and x.permissao_id = n.id);
