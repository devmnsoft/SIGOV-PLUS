-- ============================================================================
-- SIGOV PLUS · RC-SAAS-AUT · Gate B — fixtures de autorização (idempotentes)
-- ============================================================================
-- Pré-requisito: o seed de cenário `20261005_rcsaas_aut_seed.sql` já aplicado ao
-- banco de homologação (tenants 910001..910004 + assinaturas + contratos de módulo
-- + usuários hom.*.admin + perfis GTB_HOM_ADMIN).
--
-- Este fixture é COMPLEMENTAR e NÃO altera o seed de cenário publicado. Resolve dois
-- pontos necessários para a prova runtime do Gate B:
--   1) Perfil per-tenant `ADMIN_TENANT` para cada tenant de cenário (910001..910004),
--      com todas as permissões ativas concedidas no escopo do PRÓPRIO tenant. Garante
--      que os tenants saudáveis (A e D) passem pela camada de permissão de operação
--      (HasPermissionAsync) além da camada comercial de módulo (RequireModule). Sem
--      isso, o usuário passava no módulo mas falhava na operação (SEM_PERMISSAO)
--      porque o perfil compartilhado GTB_HOM_ADMIN (tenant_id=1) não cobre o escopo
--      91000x no momento da requisição.
--   2) Usuário sonda `gate.nao.admin` (tenant 910001) sem as chaves de administração
--      global (`saas.plataforma.administrar` / `cliente.usuarios.gerenciar`), para
--      demonstrar o 403 padronizado SEM_PERMISSAO no surface Web em paridade com a API.
--      O reencaminhar do vínculo de grupos é restrito aos grupos de cenário
--      (910401..910404); o grupo da sonda (910410) é fixado explicitamente em GTB_OPERADOR.
--
-- Idempotência: select-or-insert + ON CONFLICT DO NOTHING; reexecução é segura.
-- Dados fictícios; sem senha em texto puro (hash PBKDF2 documentado no seed de cenário);
-- sem dado pessoal real.
-- ============================================================================
set search_path to sigov;

-- ----------------------------------------------------------------------------
-- 1) Perfis ADMIN_TENANT per-tenant (escopo 91000x) com todas as permissões ativas
-- ----------------------------------------------------------------------------
do $gateb_profiles$
declare v_t bigint; v_p bigint;
begin
  for v_t in values (910001),(910002),(910003),(910004) loop
    select id into v_p from {{TBL}} where codigo_externo='ADMIN_TENANT' and tenant_id=v_t order by id limit 1;
    if v_p is null then
      insert into {{TBL}}(tenant_id, nome, descricao, codigo_externo, ativo, is_deleted, sistemico)
        values (v_t, 'Administrador de Tenant', 'Gate B per-tenant RC-SAAS-AUT', 'ADMIN_TENANT', true, false, false)
        returning id into v_p;
    end if;
    insert into sigov.perfil_permissao({{COL}}, permissao_id)
      select v_p, p.id from sigov.permissao p where p.ativo and not p.is_deleted
      on conflict do nothing;
  end loop;
end
$gateb_profiles$;

-- Reencaminha SOMENTE os grupos de cenário (910401..910404) para o ADMIN_TENANT do
-- próprio tenant, substituindo o vínculo ao perfil compartilhado GTB_HOM_ADMIN (t=1).
-- O grupo da sonda nao-admin (910410) fica fora deste escopo de propósito.
update sigov.grupo_perfil gp set {{COL}} = sub.id
  from (select id, tenant_id from {{TBL}} where codigo_externo='ADMIN_TENANT' and tenant_id in (910001,910002,910003,910004)) sub
 where gp.tenant_id = sub.tenant_id and gp.grupo_acesso_id in (910401,910402,910403,910404);

-- ----------------------------------------------------------------------------
-- 2) Usuário sonda nao-admin `gate.nao.admin` (tenant 910001) + perfil sem admin global
-- ----------------------------------------------------------------------------
do $gateb_nao$
declare v_prof bigint;
begin
  select id into v_prof from {{TBL}} where codigo_externo='GTB_OPERADOR' and tenant_id=910001 order by id limit 1;
  if v_prof is null then
    insert into {{TBL}}(tenant_id, nome, descricao, codigo_externo, ativo, is_deleted, sistemico)
      values (910001, 'Operador (sem admin global)', 'Gate B sonda nao-admin RC-SAAS-AUT', 'GTB_OPERADOR', true, false, false)
      returning id into v_prof;
  end if;
  insert into sigov.perfil_permissao({{COL}}, permissao_id)
    select v_prof, p.id from sigov.permissao p
    where p.ativo and not p.is_deleted
      and p.chave not in ('saas.plataforma.administrar','cliente.usuarios.gerenciar')
    on conflict do nothing;
end
$gateb_nao$;

insert into sigov.usuario(id, tenant_id, login, email, senha_hash, mfa_habilitado, ativo, is_deleted, created_at,
  senha_deve_ser_alterada, tentativas_invalidas, tipo_usuario, bloqueado, deve_alterar_senha, entidade_id, exercicio_id, observacao)
overriding system value
values (910310, 910001, 'gate.nao.admin', 'gate.nao.admin@sigov.local',
  'SIGOV_PBKDF2_V1$100000$AAECAwQFBgcICQoLDA0ODw==$UWERdQEOwXFwyYZSOKe0s++DbVwJBc2TVeKBMJvxQx0=',
  false, true, false, now(), false, 0, 'OPERADOR', false, false, 1, 1,
  'Gate B sonda nao-admin (sem admin global) - re-execucao RC-SAAS-AUT')
on conflict (id) do nothing;
update sigov.usuario set ativo=true, bloqueado=false, is_deleted=false where id=910310;

insert into sigov.grupo_acesso(id, tenant_id, nome, codigo_externo) overriding system value
  values (910410, 910001, 'Gate B Operadores (sem admin global)', 'GTB_OPERADOR_GRP')
on conflict (id) do nothing;

-- Vínculo da sonda: grupo 910410 -> GTB_OPERADOR (fixado; remove vínculo alternativo).
delete from sigov.grupo_perfil where grupo_acesso_id=910410;
insert into sigov.grupo_perfil(grupo_acesso_id, {{COL}})
  select g.id, pf.id from sigov.grupo_acesso g join {{TBL}} pf
    on pf.codigo_externo='GTB_OPERADOR' and pf.tenant_id=910001
  where g.id=910410;

insert into sigov.usuario_grupo(usuario_id, grupo_acesso_id) values (910310, 910410) on conflict do nothing;

-- ----------------------------------------------------------------------------
-- Verificação pós-aplicação (informativa)
-- ----------------------------------------------------------------------------
\echo === Gate B fixtures: permissoes por perfil (esperado 1710 em ADMIN_TENANT; ~1708 em GTB_OPERADOR) ===
select pa.tenant_id, pa.codigo_externo, count(distinct pp.permissao_id) as perms
  from {{TBL}} pa
  left join sigov.perfil_permissao pp on pp.{{COL}}=pa.id
 where pa.codigo_externo in ('ADMIN_TENANT','GTB_OPERADOR')
   and pa.tenant_id in (910001,910002,910003,910004)
 group by pa.tenant_id, pa.codigo_externo order by pa.tenant_id, pa.codigo_externo;
\echo === Gate B fixtures: vinculo dos grupos (sonda 910410 deve -> GTB_OPERADOR) ===
select gp.grupo_acesso_id, gp.{{COL}} as perfil_id, pa.codigo_externo as perfil_cod
  from sigov.grupo_perfil gp join {{TBL}} pa on pa.id=gp.{{COL}}
 where gp.grupo_acesso_id in (910401,910402,910403,910404,910410) order by gp.grupo_acesso_id;
\echo === Gate B fixtures: sonda nao-admin (mns deve ser 0) ===
select u.login,
  (select count(*) from usuario_grupo ug join grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id
     join perfil_permissao pp on pp.{{COL}}=gp.{{COL}}
     join permissao q on q.id=pp.permissao_id where ug.usuario_id=u.id) as perms,
  (select count(*) from usuario_grupo ug join grupo_perfil gp on gp.grupo_acesso_id=ug.grupo_acesso_id
     join perfil_permissao pp on pp.{{COL}}=gp.{{COL}}
     join permissao q on q.id=pp.permissao_id where ug.usuario_id=u.id and q.chave='saas.plataforma.administrar') as mns
from usuario u where u.id=910310;

