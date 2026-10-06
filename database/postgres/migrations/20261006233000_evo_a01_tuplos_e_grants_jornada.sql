-- RC-EVO (Bloco A) - Corretiva de catalogo de permissoes e grants de jornada.
-- Idempotente; banco como fonte de autoridade (regra 11); falha explicita em autoverificacao (regra 13).
-- Nao altera migrations publicadas (regra 8); apenas normaliza tuplos insatisfazeis e concede
-- chaves avaliadas em codigo que nao possuiam concessao aplicavel (fail-closed implicito).
--
-- Raizes (auditoria do Bloco A sobre o banco vivo):
--   T1/T2 'financeiro.exportar' persistido com acao vazia e 'rh.exportar' com acao='exportar',
--         enquanto o codigo avalia acao='visualizar' -> PERMITIR jamais satisfazeis.
--   D2    Duplicatas ativas exatas (modulo,recurso,acao) nos modulos de jornada -> desativa as de maior id.
--   D3    Reparacao do efeito colateral de D2: chaves canonicas exigidas por postconditions publicadas
--         (RC50.87 / corr_postconditions) voltam a ficar ativas - contrato imutavel das migrations publicadas.
--   G1    Perfis locais de administracao ganham jornada operante (escopo do proprio tenant):
--         financeiro, compras_empresariais, licitapro (compras), rh, contexto.empresa.visualizar,
--         saas.superadmin.dashboard.visualizar/exportar (SEM administrar).
--   G2    Cadeia plataforma (ADMIN_PLATAFORMA_MNSOFT, escopo tenant_id NULL) ganha o mesmo
--         conjunto operacional para a administracao global operar em qualquer tenant.
--   G4    Perfis locais de operacao nao-sistema (COORDENADOR/SERVIDOR) ganham operacao de
--         compras_empresariais (sem configuracao sensivel) e leitura/exportacao de licitapro.

do $$
declare
    v_pa_plataforma bigint;
begin
    -- T1/T2) Normalizacao pontual dos tuplos insatisfazeis. O gatilho trg_permissao_normalizar_recurso_acao
    --        so age quando recurso e nulo/vazio ou ('geral','visualizar'); aqui o recurso ja esta definido.
    update sigov.permissao set acao = 'visualizar'
     where chave = 'financeiro.exportar'
       and (acao is null or btrim(acao) = '')
       and ativo and not is_deleted;

    update sigov.permissao set acao = 'visualizar'
     where chave = 'rh.exportar'
       and btrim(coalesce(acao, '')) = 'exportar'
       and ativo and not is_deleted;

    -- D2) Duplicatas exatas ativas nos modulos de jornada: mantem o menor id e desativa os demais.
    update sigov.permissao d
       set ativo = false
      from (select id,
                   row_number() over (partition by modulo, coalesce(recurso, ''), coalesce(acao, '') order by id) as rn
              from sigov.permissao
             where ativo and not is_deleted
               and modulo in ('financeiro', 'rh', 'compras', 'compras_empresariais', 'saas')) x
     where d.id = x.id
       and x.rn > 1;

    -- D3) Reparacao idempotente do efeito colateral de D2: ao desativar duplicatas exatas, D2 tambem
    --     desativou MNSOFT_SUPERADMIN_ACCESS (id historico 1535), chave canonica exigida como ATIVA
    --     pelo postCondition publicado em 20260903100000 (RC50.87/corr_postconditions). Reativa o
    --     conjunto exigido por aquele contrato (sem alterar a migration publicada).
    update sigov.permissao
       set ativo = true, is_deleted = false, updated_at = now()
      where lower(chave) in (lower('ATIVOS_DASHBOARD_VIEW'), lower('CIDADAO_PORTAL_VIEW'), lower('SST_DASHBOARD_VIEW'),
                             lower('ACS_DASHBOARD_VIEW'), lower('GED_DASHBOARD_VIEW'), lower('GED_DOCUMENTO_SENSIVEL_VIEW'),
                             lower('COMPRAS_DASHBOARD_VIEW'), lower('MNSOFT_SUPERADMIN_ACCESS'),
                             lower('MEIO_AMBIENTE_DASHBOARD_VIEW'), lower('frotas.veiculo.visualizar'));

    -- As triggers trg_* derivam tenant_id quando NULL (do perfil/grupo pai); sao desabilitadas
    -- apenas durante os upserts (DDL transacional: em falha, o rollback restaura o estado anterior).
    alter table sigov.perfil_permissao disable trigger user;

    -- G1) Grants de jornada para perfis locais de administracao.
    --     Perfis sistemicos sao compartilhados entre tenants: o escopo fica NULL na linha
    --     perfil_permissao e a restricao ao tenant efetivo vem das linhas usuario_grupo /
    --     grupo_perfil do usuario (mesmo padrao da cadeia plataforma da migration 05).
    --     Perfis nao-sistema pertencem a um unico tenant: mantem o tenant do perfil.
    insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, tenant_id, entidade_id, exercicio_id, unidade_id, efeito, alcada_valor, justificativa, ativo, is_deleted, created_at, created_by, updated_at)
    select distinct pa.id, p.id, case when pa.sistemico then null::bigint else pa.tenant_id end, null::bigint, null::bigint, null::bigint, 'PERMITIR', null::numeric,
           'RC-EVO (Bloco A): jornada operante para administracao local do tenant',
           true, false, now(), null::bigint, now()
      from sigov.perfil_acesso pa
      join sigov.permissao p
        on p.ativo and not p.is_deleted
       and (p.modulo = 'compras_empresariais'
            or (p.modulo = 'rh' and p.chave like 'rh.%')
            or p.modulo = 'licitapro'
            or (p.modulo = 'compras' and p.chave ilike '%licitapro%')
            or p.modulo = 'financeiro'
            or p.chave in ('contexto.empresa.visualizar',
                           'saas.superadmin.dashboard.visualizar',
                           'saas.superadmin.dashboard.exportar'))
     where pa.tenant_id is not null
       and pa.ativo and not pa.is_deleted
       and pa.codigo_externo in ('ADMIN_TENANT', 'ADMINISTRADOR_TENANT', 'ADMIN_GERAL', 'ADMINISTRADOR_GERAL')
    on conflict (perfil_acesso_id, permissao_id) do update
        set tenant_id = excluded.tenant_id, entidade_id = null, exercicio_id = null, unidade_id = null,
            vigencia_inicio = null, vigencia_fim = null, efeito = 'PERMITIR', alcada_valor = null,
            ativo = true, is_deleted = false, updated_at = now();

    -- G2) Grants de jornada para a cadeia plataforma (escopo tenant_id NULL) - administracao global.
    select pa.id into strict v_pa_plataforma
      from sigov.perfil_acesso pa
     where pa.codigo_externo = 'ADMIN_PLATAFORMA_MNSOFT'
       and pa.ativo and not pa.is_deleted
     order by pa.id
     limit 1;

    insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, tenant_id, entidade_id, exercicio_id, unidade_id, efeito, alcada_valor, justificativa, ativo, is_deleted, created_at, created_by, updated_at)
    select distinct v_pa_plataforma, p.id, null::bigint, null::bigint, null::bigint, null::bigint, 'PERMITIR', null::numeric,
           'RC-EVO (Bloco A): jornada operante para administracao global (escopo plataforma)',
           true, false, now(), null::bigint, now()
      from sigov.permissao p
     where p.ativo and not p.is_deleted
       and (p.modulo = 'compras_empresariais'
            or (p.modulo = 'rh' and p.chave like 'rh.%')
            or p.modulo = 'licitapro'
            or (p.modulo = 'compras' and p.chave ilike '%licitapro%')
            or p.modulo = 'financeiro')
    on conflict (perfil_acesso_id, permissao_id) do update
        set tenant_id = null, entidade_id = null, exercicio_id = null, unidade_id = null,
            vigencia_inicio = null, vigencia_fim = null, efeito = 'PERMITIR', alcada_valor = null,
            ativo = true, is_deleted = false, updated_at = now();

    -- G4) Grants de operacao para perfis locais nao-sistema (sem configuracao sensivel de fornecedores).
    insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, tenant_id, entidade_id, exercicio_id, unidade_id, efeito, alcada_valor, justificativa, ativo, is_deleted, created_at, created_by, updated_at)
    select distinct pa.id, p.id, pa.tenant_id, null::bigint, null::bigint, null::bigint, 'PERMITIR', null::numeric,
           'RC-EVO (Bloco A): operacao de jornada para perfis locais de coordenacao/servidor',
           true, false, now(), null::bigint, now()
      from sigov.perfil_acesso pa
      join sigov.permissao p
        on p.ativo and not p.is_deleted
       and ((p.modulo = 'compras_empresariais'
             and p.chave not in ('compras_empresariais.configuracao.gerenciar',
                                 'compras_empresariais.fornecedores.dados_bancarios',
                                 'compras_empresariais.avaliacoes.gerenciar'))
            or (p.modulo = 'licitapro' and p.acao in ('visualizar', 'exportar'))
            or (p.modulo = 'compras' and p.chave ilike '%licitapro%' and p.acao in ('visualizar', 'exportar')))
     where pa.tenant_id is not null
       and coalesce(pa.sistemico, false) = false
       and pa.ativo and not pa.is_deleted
       and pa.codigo_externo in ('COORDENADOR', 'SERVIDOR')
    on conflict (perfil_acesso_id, permissao_id) do update
        set tenant_id = excluded.tenant_id, entidade_id = null, exercicio_id = null, unidade_id = null,
            vigencia_inicio = null, vigencia_fim = null, efeito = 'PERMITIR', alcada_valor = null,
            ativo = true, is_deleted = false, updated_at = now();

    alter table sigov.perfil_permissao enable trigger user;

    -- Autoverificacao (falha explicita; regra 13).
    if not exists (select 1 from sigov.permissao
                   where chave = 'financeiro.exportar' and btrim(coalesce(acao, '')) = 'visualizar' and ativo and not is_deleted)
       or not exists (select 1 from sigov.permissao
                      where chave = 'rh.exportar' and btrim(coalesce(acao, '')) = 'visualizar' and ativo and not is_deleted)
    then
        raise exception 'RC-EVO A01: tuplos de exportacao nao ficaram satisfeitos (financeiro.exportar / rh.exportar com acao=visualizar)';
    end if;

    if not exists (
        select 1 from sigov.perfil_permissao pp
        join sigov.perfil_acesso pa on pa.id = pp.perfil_acesso_id
        join sigov.permissao p on p.id = pp.permissao_id
        where pa.codigo_externo = 'ADMIN_PLATAFORMA_MNSOFT'
          and pp.tenant_id is null
          and upper(pp.efeito) = 'PERMITIR'
          and pp.ativo and not pp.is_deleted
          and p.ativo and not p.is_deleted
          and p.modulo in ('compras_empresariais', 'financeiro'))
    then
        raise exception 'RC-EVO A01: cadeia plataforma sem grants operacionais de escopo NULL';
    end if;

    if exists (select 1 from sigov.perfil_acesso
               where tenant_id is not null and ativo and not is_deleted
                 and codigo_externo in ('ADMIN_TENANT', 'ADMINISTRADOR_TENANT'))
       and not exists (
           select 1 from sigov.perfil_permissao pp
           join sigov.perfil_acesso pa on pa.id = pp.perfil_acesso_id
           join sigov.permissao p on p.id = pp.permissao_id
           where pa.tenant_id is not null
             and pa.codigo_externo in ('ADMIN_TENANT', 'ADMINISTRADOR_TENANT')
             and upper(pp.efeito) = 'PERMITIR'
             and pp.ativo and not pp.is_deleted
             and p.ativo and not p.is_deleted
             and p.modulo = 'compras_empresariais')
    then
        raise exception 'RC-EVO A01: perfis locais de administracao sem grants de jornada';
    end if;

    if exists (
        select 1 from (values ('ATIVOS_DASHBOARD_VIEW'), ('CIDADAO_PORTAL_VIEW'), ('SST_DASHBOARD_VIEW'),
                              ('ACS_DASHBOARD_VIEW'), ('GED_DASHBOARD_VIEW'), ('GED_DOCUMENTO_SENSIVEL_VIEW'),
                              ('COMPRAS_DASHBOARD_VIEW'), ('MNSOFT_SUPERADMIN_ACCESS'),
                              ('MEIO_AMBIENTE_DASHBOARD_VIEW'), ('frotas.veiculo.visualizar')) required(chave)
       where not exists (select 1 from sigov.permissao p
                          where lower(p.chave) = lower(required.chave) and p.ativo and not p.is_deleted)
    ) then
        raise exception 'RC-EVO A01: chaves canonicas exigidas pelo postcondition publicado nao ficaram ativas';
    end if;

    raise notice 'RC-EVO A01 concluida: tuplos normalizados, chaves canonicas reparadas e grants de jornada aplicados.';
end $$;
