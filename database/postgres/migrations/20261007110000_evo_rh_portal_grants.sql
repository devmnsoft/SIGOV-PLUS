-- RC-EVO-RH s8 | 20261007110000 - Grants do espelho de folha no portal RH (idempotente).
-- O espelho de folha no portal (s8) resolve o vinculo usuario->servidor no servidor
-- (sigov.rh_portal_usuario) e exige a permissao rh.portal.visualizar (catalogo id 62).
-- Hoje apenas a cadeia de administracao (ADMIN_TENANT/ADMINISTRADOR_GERAL/
-- ADMIN_PLATAFORMA_MNSOFT) tem PERMITIR; o servidor comum nao consulta o proprio
-- espelho. Esta migration concede rh.portal.visualizar PERMITIR aos perfis locais
-- nao-sistema SERVIDOR e COORDENADOR (escopo do proprio tenant), mantendo o banco
-- como fonte de autoridade (regra 11). Sem auto-aprovacao: o portal continua somente
-- leitura do proprio escopo - ninguem decide nada por ele (sections 5/6).
-- Idempotente via arbiter (perfil_acesso_id, permissao_id); triggers user desligadas
-- apenas durante o upsert (DDL transacional: em falha, o rollback restaura o estado).
-- Autoverificacao com falha explicita (regra 13); sem dado pessoal real (regra 10).

do $grants$
begin
    -- Precondicao explicita: a permissao deve existir no catalogo (regra 13).
    if not exists (select 1 from sigov.permissao
                   where chave = 'rh.portal.visualizar' and ativo and not is_deleted)
    then
        raise exception 'RC-EVO-RH 20261007110000: permissao rh.portal.visualizar ausente do catalogo';
    end if;

    alter table sigov.perfil_permissao disable trigger user;

    -- G1) Grants de leitura do proprio espelho para perfis locais nao-sistema.
    --     Perfis nao-sistema pertencem a um unico tenant: a linha perfil_permissao
    --     mantem o tenant do perfil (escopo proprio). Serao concedidos em todos os
    --     tenants que possuirem esses perfis ativos - regra genericamente multi-esfera
    --     (regras 21-24), sem tenant hardcoded.
    insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, tenant_id, entidade_id, exercicio_id, unidade_id, efeito, alcada_valor, justificativa, ativo, is_deleted, created_at, created_by, updated_at)
    select distinct pa.id, p.id, pa.tenant_id, null::bigint, null::bigint, null::bigint, 'PERMITIR', null::numeric,
           'RC-EVO-RH s8: leitura do proprio espelho de folha/portal (contracheques e pendencias)',
           true, false, now(), null::bigint, now()
      from sigov.perfil_acesso pa
      join sigov.permissao p
        on p.ativo and not p.is_deleted
       and p.chave = 'rh.portal.visualizar'
     where pa.tenant_id is not null
       and coalesce(pa.sistemico, false) = false
       and pa.ativo and not pa.is_deleted
       and pa.codigo_externo in ('COORDENADOR', 'SERVIDOR')
     on conflict (perfil_acesso_id, permissao_id) do update
         set tenant_id = excluded.tenant_id, entidade_id = null, exercicio_id = null, unidade_id = null,
             vigencia_inicio = null, vigencia_fim = null, efeito = 'PERMITIR', alcada_valor = null,
             ativo = true, is_deleted = false, updated_at = now();

    alter table sigov.perfil_permissao enable trigger user;

    -- Autoverificacao (falha explicita; regra 13): nenhum perfil local ativo ficou sem o grant.
    if exists (select 1
               from sigov.perfil_acesso pa
               where pa.tenant_id is not null
                 and coalesce(pa.sistemico, false) = false
                 and pa.ativo and not pa.is_deleted
                 and pa.codigo_externo in ('COORDENADOR', 'SERVIDOR')
                 and not exists (select 1
                                 from sigov.perfil_permissao pp
                                 join sigov.permissao p on p.id = pp.permissao_id
                                 where pp.perfil_acesso_id = pa.id
                                   and upper(pp.efeito) = 'PERMITIR'
                                   and pp.ativo and not pp.is_deleted
                                   and p.chave = 'rh.portal.visualizar'
                                   and p.ativo and not p.is_deleted))
    then
        raise exception 'RC-EVO-RH 20261007110000: algum perfil SERVIDOR/COORDENADOR ficou sem PERMITIR em rh.portal.visualizar';
    end if;

    raise notice 'RC-EVO-RH 20261007110000 concluida: rh.portal.visualizar PERMITIR para perfis locais SERVIDOR e COORDENADOR.';
end $grants$;
