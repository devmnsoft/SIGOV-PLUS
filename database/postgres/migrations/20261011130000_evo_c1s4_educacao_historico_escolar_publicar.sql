-- ============================================================================
-- RC-EVO Ciclo 1 Sprint 4 (educacao/notas e boletim)
-- Permissão granular educacao.historico_escolar.publicar + concessão aos perfis
-- administrativos (mesmo padrão de seed do RC50.29: ADMINISTRADOR_GERAL /
-- ADMINISTRADOR_TENANT / SUPERADMIN / *ADMINISTRADOR*), por tenant.
-- Idempotente (on conflict); multi-esfera (regra 24); banco como autoridade
-- de autorização (regras 11/12).
-- ============================================================================

do $permissions$
declare permission_key text := 'educacao.historico_escolar.publicar';
begin
  insert into sigov.permissao(chave,descricao,modulo,recurso,acao,ativo)
  values(permission_key,'Publicar histórico escolar/boletim (Ciclo 1 Sprint 4)','educacao','historico_escolar','publicar',true)
  on conflict (chave) do update set ativo=true,is_deleted=false;

  insert into sigov.perfil_permissao(tenant_id,perfil_acesso_id,permissao_id)
  select coalesce(pa.tenant_id,t.id),pa.id,p.id
  from sigov.perfil_acesso pa
  cross join sigov.tenant t
  join sigov.permissao p on p.chave=permission_key
  where pa.ativo and not pa.is_deleted and t.ativo and not t.is_deleted
    and (upper(coalesce(pa.codigo_externo,'')) in ('ADMINISTRADOR_GERAL','ADMINISTRADOR_TENANT','SUPERADMIN') or upper(pa.nome) like '%ADMINISTRADOR%')
    and (pa.tenant_id is null or pa.tenant_id=t.id)
  on conflict do nothing;
end $permissions$;

-- Post-condição explícita (regra 13): sem permissão ativa e sem concessão, falha nomeada.
do $post$
begin
  if not exists(select 1 from sigov.permissao where chave='educacao.historico_escolar.publicar' and ativo and not is_deleted) then
    raise exception 'S4-POSTCOND: permissao educacao.historico_escolar.publicar ausente ou inativa';
  end if;
  if not exists(
    select 1 from sigov.perfil_permissao pp
    join sigov.permissao p on p.id=pp.permissao_id
    where p.chave='educacao.historico_escolar.publicar' and pp.ativo and not pp.is_deleted and pp.efeito='PERMITIR'
  ) then
    raise exception 'S4-POSTCOND: sem concessao PERMITIR da permissao educacao.historico_escolar.publicar';
  end if;
end $post$;
