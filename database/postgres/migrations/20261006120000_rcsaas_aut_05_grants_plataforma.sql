-- RC-SAAS-AUT Etapa B - Corretiva: grants de escopo plataforma (tenant_id IS NULL) para a
-- administracao global MNSOFT do dashboard SaaS (Web) e da API comercial.
-- Root cause (Gates H/I): o SaasAdminController (Web) avalia ('saas','saas.superadmin.dashboard',acao)
-- com TenantId = tenant ALVO e ('saas','saas.superadmin.autorizacao','administrar') com TenantId NULL,
-- mas os grants dos administradores globais estao escopados ao proprio tenant; o avaliador canonico
-- so casa linhas com (tenant_id IS NULL OR tenant_id = @TenantId) e nao existia nenhuma linha de
-- escopo plataforma (zero rows com tenant_id NULL nas tabelas de vinculo).
-- ADEC: ok=false motivo=SEMCONCESSAOAPLICAVEL origem=WEB_SUPERADMIN_DASHBOARD.
-- Correcao: cadeia de vinculo dedicada de escopo plataforma (grupo/perfil/vinculos com tenant_id NULL)
-- para os logins admin e superadmin, cobrindo as chaves avaliadas pela Web
-- (saas.superadmin.dashboard/{visualizar,administrar,exportar} e saas.superadmin.autorizacao/administrar)
-- e pela API comercial (saas.plataforma/administrar). Banco como fonte de autoridade (regra 11);
-- idempotente; tenant_id da cadeia passa a admitir NULL (escopo plataforma; DDL idempotente);
-- triggers de preenchimento de tenant desabilitadas apenas durante os upserts
-- (DDL transacional: em falha o rollback restaura o estado anterior).

do $$
declare
    v_chaves text[] := array[
        'saas.plataforma.administrar',
        'saas.superadmin.autorizacao.administrar',
        'saas.superadmin.dashboard.administrar',
        'saas.superadmin.dashboard.exportar',
        'saas.superadmin.dashboard.visualizar'
    ];
    v_ga_id bigint;
    v_pa_id bigint;
    v_users integer;
    v_ok integer;
begin
    -- 1a) Escopo plataforma exige tenant_id anulavel na cadeia (DDL idempotente; no-op onde ja e anulavel).
    alter table sigov.grupo_acesso alter column tenant_id drop not null;
    alter table sigov.grupo_perfil alter column tenant_id drop not null;
    alter table sigov.perfil_permissao alter column tenant_id drop not null;
    alter table sigov.usuario_grupo alter column tenant_id drop not null;

    -- 1) Usuarios-alvo e catalogo completos e ativos (falha explicita; regra 13).
    select count(*) into v_users from sigov.usuario where lower(login) in ('admin', 'superadmin') and ativo and not is_deleted;
    if v_users = 0 then
        raise exception 'RC-SAAS-AUT grants plataforma: nenhum usuario ativo com login admin/superadmin';
    end if;

    if (select count(distinct chave) from sigov.permissao where modulo = 'saas' and chave = any (v_chaves) and ativo and not is_deleted) <> cardinality(v_chaves) then
        raise exception 'RC-SAAS-AUT grants plataforma: chaves ausentes/inativas no catalogo (% de %)',
            (select count(distinct chave) from sigov.permissao where modulo = 'saas' and chave = any (v_chaves) and ativo and not is_deleted),
            cardinality(v_chaves);
    end if;

    -- 2) Grupo e perfil de escopo plataforma (tenant_id NULL).
    select id into v_ga_id from sigov.grupo_acesso where nome = 'Plataforma MNSOFT' and tenant_id is null order by id limit 1;
    if v_ga_id is null then
        insert into sigov.grupo_acesso (nome, descricao, codigo_externo, ativo, is_deleted, created_at, created_by)
        values ('Plataforma MNSOFT',
                'Grupo global de administracao da plataforma SaaS MNSOFT (escopo multi-tenant, sem tenant)',
                'PLATAFORMA_MNSOFT', true, false, now(), null)
        returning id into v_ga_id;
    else
        update sigov.grupo_acesso set ativo = true, is_deleted = false, updated_at = now() where id = v_ga_id;
    end if;

    select id into v_pa_id from sigov.perfil_acesso where codigo_externo = 'ADMIN_PLATAFORMA_MNSOFT' order by is_deleted, ativo desc, id desc limit 1;
    if v_pa_id is null then
        insert into sigov.perfil_acesso (nome, descricao, codigo_externo, sistemico, ativo, is_deleted, created_at, created_by)
        values ('Administrador de Plataforma MNSOFT',
                'Perfil global da administracao SaaS MNSOFT: dashboard de clientes, usuarios do cliente e acoes comerciais',
                'ADMIN_PLATAFORMA_MNSOFT', true, true, false, now(), null)
        returning id into v_pa_id;
    else
        update sigov.perfil_acesso set ativo = true, is_deleted = false, sistemico = true, updated_at = now() where id = v_pa_id;
    end if;

    -- 3) Vinculos de escopo plataforma (tenant_id NULL). As triggers trg_*_tenant derivam
    -- tenant_id quando NULL (do usuario/grupo/perfil pai) e bloqueiam valores nao resolvel;
    -- sao desabilitadas somente durante estes upserts.
    alter table sigov.usuario_grupo disable trigger user;
    alter table sigov.grupo_perfil disable trigger user;
    alter table sigov.perfil_permissao disable trigger user;

    insert into sigov.grupo_perfil (grupo_acesso_id, perfil_acesso_id, tenant_id, entidade_id, exercicio_id, unidade_id, vigencia_inicio, vigencia_fim, ativo, is_deleted, created_at, created_by)
    values (v_ga_id, v_pa_id, null, null, null, null, null, null, true, false, now(), null)
    on conflict (grupo_acesso_id, perfil_acesso_id) do update
        set tenant_id = null, entidade_id = null, exercicio_id = null, unidade_id = null,
            vigencia_inicio = null, vigencia_fim = null, ativo = true, is_deleted = false;

    insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id, tenant_id, entidade_id, exercicio_id, unidade_id, efeito, alcada_valor, justificativa, ativo, is_deleted, created_at, created_by, updated_at)
    select v_pa_id, p.id, null, null, null, null, 'PERMITIR', null,
           'RC-SAAS-AUT: escopo plataforma multi-tenant para administracao global SaaS MNSOFT',
           true, false, now(), null, now()
    from sigov.permissao p
    where p.modulo = 'saas' and p.chave = any (v_chaves) and p.ativo and not p.is_deleted
    on conflict (perfil_acesso_id, permissao_id) do update
        set tenant_id = null, entidade_id = null, exercicio_id = null, unidade_id = null,
            vigencia_inicio = null, vigencia_fim = null, efeito = 'PERMITIR', alcada_valor = null,
            ativo = true, is_deleted = false, updated_at = now();

    insert into sigov.usuario_grupo (usuario_id, grupo_acesso_id, tenant_id, entidade_id, exercicio_id, unidade_id, vigencia_inicio, vigencia_fim, ativo, is_deleted, created_at, created_by)
    select u.id, v_ga_id, null, null, null, null, null, null, true, false, now(), null
    from sigov.usuario u
    where lower(u.login) in ('admin', 'superadmin') and u.ativo and not u.is_deleted
    on conflict (usuario_id, grupo_acesso_id) do update
        set tenant_id = null, entidade_id = null, exercicio_id = null, unidade_id = null,
            vigencia_inicio = null, vigencia_fim = null, ativo = true, is_deleted = false;

    alter table sigov.usuario_grupo enable trigger user;
    alter table sigov.grupo_perfil enable trigger user;
    alter table sigov.perfil_permissao enable trigger user;

    -- 4) Autoverificacao: cada usuario-alvo deve cobrir as 5 chaves via cadeia plataforma.
    select count(*) into v_ok from (
        select distinct u.id, p.chave
        from sigov.usuario u
        join sigov.usuario_grupo ug on ug.usuario_id = u.id and ug.ativo and not ug.is_deleted
        join sigov.grupo_acesso ga on ga.id = ug.grupo_acesso_id and ga.ativo and not ga.is_deleted
        join sigov.grupo_perfil gp on gp.grupo_acesso_id = ga.id and gp.ativo and not gp.is_deleted
        join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id and pa.ativo and not pa.is_deleted
        join sigov.perfil_permissao pp on pp.perfil_acesso_id = pa.id and pp.ativo and not pp.is_deleted
        join sigov.permissao p on p.id = pp.permissao_id and p.ativo and not p.is_deleted
        where lower(u.login) in ('admin', 'superadmin') and u.ativo and not u.is_deleted
          and p.modulo = 'saas' and p.chave = any (v_chaves)
          and ug.tenant_id is null and gp.tenant_id is null and pp.tenant_id is null
          and upper(pp.efeito) = 'PERMITIR'
    ) x;
    if v_ok <> v_users * cardinality(v_chaves) then
        raise exception 'RC-SAAS-AUT grants plataforma: autoverificacao falhou (cobertura % <> esperada %)',
            v_ok, v_users * cardinality(v_chaves);
    end if;
end $$;
