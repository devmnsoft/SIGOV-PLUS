-- ============================================================================
-- RC-SAAS-AUT (secao 6) - Seed de homologacao: clientes SaaS ficticios
-- ----------------------------------------------------------------------------
-- Objetivo: prover 4 clientes ficticios que espelham as quatro situacoes
-- comerciais canonicas avaliadas por ModuleAccessChecker/SaasForbiddenMotivo:
--
--   Cliente A (910001) - contrato saudavel   : assinatura ATIVA + modulos HABILITADO
--                                                -> acesso permitido (baseline).
--   Cliente B (910002) - bloqueio comercial  : assinatura SUSPENSA + modulos SUSPENSO
--                                                -> motivo BLOQUEADO_COMERCIAL.
--   Cliente C (910003) - vigencia expirada   : assinatura ATIVA com data_fim no passado
--                                                -> motivo CONTRATO_EXPIRADO.
--   Cliente D (910004) - limite de usuarios  : assinatura ATIVA com usuarios_contratados=1
--                                                -> cria o 2o usuario -> LIMITE_ATINGIDO.
--
-- Credencial de homologacao (regras 9 e 10 do AGENTS.md):
--   * Todos os usuarios usam a MESMA senha FICTICIA documentada aqui:
--       login:  hom.a.admin / hom.b.admin / hom.c.admin / hom.d.admin
--       senha:  Homologacao!2026
--   * O hash abaixo e o PBKDF2-SHA256 (100.000 iteracoes, key 32 bytes) dessa
--     senha com salt deterministico 00..0F; e um dado ficticio, calculavel e
--     idempotente. Nao contem senha/token/chave/dado pessoal REAL.
--
-- Idempotencia: ids explicitos na faixa 910000+ (longe do maximo atual do
-- banco) com ON CONFLICT DO NOTHING / where-not-exists; execucoes repetidas
-- nao duplicam nem alteram estado ja presente.
-- Bloqueio em Production via current_setting('sigov.environment').
-- ============================================================================

do $$
begin
    if current_setting('sigov.environment', true) = 'Production' then
        raise exception 'Seed de homologacao rcsaas_aut_seed.sql bloqueado em Production';
    end if;
end $$;

-- Pre-condicoes explicitas (regra 13: ausencia de schema/config deve ser explicita).
do $$
begin
    if not exists(select 1 from sigov.saas_plano where id = 1) then
        raise exception 'Pre-condicao falhou: saas_plano id=1 (ESSENCIAL) ausente';
    end if;
    if not exists(select 1 from sigov.perfil_acesso where id = 50 and codigo_externo = 'ADMIN_TENANT') then
        raise exception 'Pre-condicao falhou: perfil 50 (ADMIN_TENANT) ausente';
    end if;
end $$;

-- ----------------------------------------------------------------------------
-- 1) Tenants (clientes) ficticios - multi-esfera (nomenclatura generica, regra 21)
-- ----------------------------------------------------------------------------
insert into sigov.tenant(id, nome, slug, status, timezone, locale, ambiente, metadados)
overriding system value
values
    (910001, 'Cliente Homologacao A - contrato saudavel', 'hom_a_contrato_saudavel',  'ATIVO', 'America/Sao_Paulo', 'pt-BR', 'HOMOLOGACAO', '{"origem":"seed_rcsaas_aut","sit":"contrato_saudavel"}'::jsonb),
    (910002, 'Cliente Homologacao B - bloqueio comercial','hom_b_comercial_bloqueado','ATIVO', 'America/Sao_Paulo', 'pt-BR', 'HOMOLOGACAO', '{"origem":"seed_rcsaas_aut","sit":"bloqueio_comercial"}'::jsonb),
    (910003, 'Cliente Homologacao C - vigencia expirada', 'hom_c_vigencia_expirada',  'ATIVO', 'America/Sao_Paulo', 'pt-BR', 'HOMOLOGACAO', '{"origem":"seed_rcsaas_aut","sit":"vigencia_expirada"}'::jsonb),
    (910004, 'Cliente Homologacao D - limite de usuarios','hom_d_limite_usuarios',    'ATIVO', 'America/Sao_Paulo', 'pt-BR', 'HOMOLOGACAO', '{"origem":"seed_rcsaas_aut","sit":"limite_usuarios"}'::jsonb)
on conflict (id) do nothing;

-- ----------------------------------------------------------------------------
-- 2) Assinaturas familia B (fonte canonica de plano/vigencia/limites)
-- ----------------------------------------------------------------------------
insert into sigov.saas_assinatura(
    id, tenant_id, plano_id, status, data_inicio, data_fim,
    usuarios_contratados, valor_contratado, moeda, periodicidade,
    renovacao_automatica, observacao
)
overriding system value
values
    -- A: ATIVA com vigencia futura -> baseline de acesso permitido
    (910101, 910001, 1, 'ATIVA',    DATE '2026-09-01', DATE '2027-08-31', 20, 499.90, 'BRL', 'MENSAL', false, 'Assinatura ficticia de homologacao RC-SAAS-AUT'),
    -- B: SUSPENSA (bloqueio comercial) -> BLOQUEADO_COMERCIAL
    (910102, 910002, 1, 'SUSPENSA', DATE '2026-09-01', DATE '2027-08-31', 10, 299.90, 'BRL', 'MENSAL', false, 'Suspensao comercial ficticia para homologacao RC-SAAS-AUT'),
    -- C: ATIVA mas com data_fim no passado -> CONTRATO_EXPIRADO
    (910103, 910003, 1, 'ATIVA',    DATE '2026-03-01', DATE '2026-09-30', 10, 299.90, 'BRL', 'MENSAL', false, 'Vigencia expirada ficticia para homologacao RC-SAAS-AUT'),
    -- D: ATIVA sem fim, mas limitada a 1 usuario -> LIMITE_ATINGIDO na 2a criacao
    (910104, 910004, 1, 'ATIVA',    DATE '2026-09-01', NULL,               1, 199.90, 'BRL', 'MENSAL', false, 'Limite de usuarios ficticio para homologacao RC-SAAS-AUT')
on conflict (id) do nothing;

-- ----------------------------------------------------------------------------
-- 3) Modulos da assinatura (financeiro e educacao: sem dependencias)
-- ----------------------------------------------------------------------------
insert into sigov.saas_assinatura_modulo(
    id, tenant_id, assinatura_id, modulo_codigo, status, habilitado, vigencia_inicio, vigencia_fim
)
overriding system value
values
    (910201, 910001, 910101, 'financeiro', 'ATIVO',    true,  DATE '2026-09-01', NULL),
    (910202, 910001, 910101, 'educacao',   'ATIVO',    true,  DATE '2026-09-01', NULL),
    (910203, 910002, 910102, 'financeiro', 'SUSPENSO', false, DATE '2026-09-01', NULL),
    (910204, 910002, 910102, 'educacao',   'SUSPENSO', false, DATE '2026-09-01', NULL),
    (910205, 910003, 910103, 'financeiro', 'ATIVO',    true,  DATE '2026-03-01', NULL),
    (910206, 910003, 910103, 'educacao',   'ATIVO',    true,  DATE '2026-03-01', NULL),
    (910207, 910004, 910104, 'financeiro', 'ATIVO',    true,  DATE '2026-09-01', NULL),
    (910208, 910004, 910104, 'educacao',   'ATIVO',    true,  DATE '2026-09-01', NULL)
on conflict (tenant_id, assinatura_id, modulo_codigo) do nothing;

-- ----------------------------------------------------------------------------
-- 4) Contratos de modulo (tenant_modulo_contratado) - status coerente com a situacao
-- ----------------------------------------------------------------------------
insert into sigov.tenant_modulo_contratado(
    id, tenant_id, modulo_codigo, status, contratado_em, vigencia_inicio, ciclo_cobranca, motivo_status
)
overriding system value
values
    (910501, 910001, 'financeiro', 'HABILITADO', DATE '2026-09-01', DATE '2026-09-01', 'MENSAL', NULL),
    (910502, 910001, 'educacao',   'HABILITADO', DATE '2026-09-01', DATE '2026-09-01', 'MENSAL', NULL),
    (910503, 910002, 'financeiro', 'SUSPENSO',   DATE '2026-09-01', DATE '2026-09-01', 'MENSAL', 'Suspensao comercial ficticia para homologacao RC-SAAS-AUT'),
    (910504, 910002, 'educacao',   'SUSPENSO',   DATE '2026-09-01', DATE '2026-09-01', 'MENSAL', 'Suspensao comercial ficticia para homologacao RC-SAAS-AUT'),
    (910505, 910003, 'financeiro', 'HABILITADO', DATE '2026-03-01', DATE '2026-03-01', 'MENSAL', NULL),
    (910506, 910003, 'educacao',   'HABILITADO', DATE '2026-03-01', DATE '2026-03-01', 'MENSAL', NULL),
    (910507, 910004, 'financeiro', 'HABILITADO', DATE '2026-09-01', DATE '2026-09-01', 'MENSAL', NULL),
    (910508, 910004, 'educacao',   'HABILITADO', DATE '2026-09-01', DATE '2026-09-01', 'MENSAL', NULL)
on conflict (id) do nothing;

-- ----------------------------------------------------------------------------
-- 5) Historico de assinaturas (trilha inicial da situacao de cada cliente)
-- ----------------------------------------------------------------------------
insert into sigov.saas_assinatura_historico(
    id, assinatura_id, tenant_id, plano_anterior_id, plano_novo_id, acao, motivo, correlation_id
)
overriding system value
values
    (910601, 910101, 910001, NULL, 1, 'CRIAR', 'Seed de homologacao RC-SAAS-AUT (dado ficticio)', 'e7c1a2b0-0000-4000-8000-000000910601'::uuid),
    (910602, 910102, 910002, NULL, 1, 'CRIAR', 'Seed de homologacao RC-SAAS-AUT (dado ficticio)', 'e7c1a2b0-0000-4000-8000-000000910602'::uuid),
    (910603, 910103, 910003, NULL, 1, 'CRIAR', 'Seed de homologacao RC-SAAS-AUT (dado ficticio)', 'e7c1a2b0-0000-4000-8000-000000910603'::uuid),
    (910604, 910104, 910004, NULL, 1, 'CRIAR', 'Seed de homologacao RC-SAAS-AUT (dado ficticio)', 'e7c1a2b0-0000-4000-8000-000000910604'::uuid)
on conflict (id) do nothing;

-- ----------------------------------------------------------------------------
-- 6) Usuarios ficticos (um admin por cliente; ultimo admin protegido na pratica)
--    Senha ficticia documentada: Homologacao!2026
--    hash = SIGOV_PBKDF2_V1$100000$AAECAwQFBgcICQoLDA0ODw==$UWERdQEOwXFwyYZSOKe0s++DbVwJBc2TVeKBMJvxQx0=
-- ----------------------------------------------------------------------------
insert into sigov.usuario(
    id, tenant_id, login, email, senha_hash, mfa_habilitado, ativo, is_deleted, created_at,
    senha_deve_ser_alterada, tentativas_invalidas, tipo_usuario, bloqueado, deve_alterar_senha,
    entidade_id, exercicio_id, observacao
)
overriding system value
values
    (910301, 910001, 'hom.a.admin', 'hom.a.admin@sigov.local', 'SIGOV_PBKDF2_V1$100000$AAECAwQFBgcICQoLDA0ODw==$UWERdQEOwXFwyYZSOKe0s++DbVwJBc2TVeKBMJvxQx0=', false, true, false, now(), false, 0, 'OPERADOR', false, false, 1, 1, 'Usuario ficticio de homologacao RC-SAAS-AUT (senha documentada no cabecalho do seed)'),
    (910302, 910002, 'hom.b.admin', 'hom.b.admin@sigov.local', 'SIGOV_PBKDF2_V1$100000$AAECAwQFBgcICQoLDA0ODw==$UWERdQEOwXFwyYZSOKe0s++DbVwJBc2TVeKBMJvxQx0=', false, true, false, now(), false, 0, 'OPERADOR', false, false, 1, 1, 'Usuario ficticio de homologacao RC-SAAS-AUT (senha documentada no cabecalho do seed)'),
    (910303, 910003, 'hom.c.admin', 'hom.c.admin@sigov.local', 'SIGOV_PBKDF2_V1$100000$AAECAwQFBgcICQoLDA0ODw==$UWERdQEOwXFwyYZSOKe0s++DbVwJBc2TVeKBMJvxQx0=', false, true, false, now(), false, 0, 'OPERADOR', false, false, 1, 1, 'Usuario ficticio de homologacao RC-SAAS-AUT (senha documentada no cabecalho do seed)'),
    (910304, 910004, 'hom.d.admin', 'hom.d.admin@sigov.local', 'SIGOV_PBKDF2_V1$100000$AAECAwQFBgcICQoLDA0ODw==$UWERdQEOwXFwyYZSOKe0s++DbVwJBc2TVeKBMJvxQx0=', false, true, false, now(), false, 0, 'OPERADOR', false, false, 1, 1, 'Usuario ficticio de homologacao RC-SAAS-AUT (senha documentada no cabecalho do seed)')
on conflict (id) do nothing;

-- Re-execucao: mantem os usuarios da homologacao operacionais (sem tocar em
-- updated_at para nao revogar sessoes derivadas da versao de identidade).
update sigov.usuario set ativo = true, bloqueado = false, is_deleted = false
where id in (910301, 910302, 910303, 910304);

-- ----------------------------------------------------------------------------
-- 7) Grupos e vinculos de perfil (admin do tenant via perfil sistematico 50)
-- ----------------------------------------------------------------------------
insert into sigov.grupo_acesso(id, tenant_id, nome, codigo_externo, observacao)
overriding system value
values
    (910401, 910001, 'Administradores do cliente (homologacao)', 'HOM_ADMIN', 'Grupo ficticio de homologacao RC-SAAS-AUT'),
    (910402, 910002, 'Administradores do cliente (homologacao)', 'HOM_ADMIN', 'Grupo ficticio de homologacao RC-SAAS-AUT'),
    (910403, 910003, 'Administradores do cliente (homologacao)', 'HOM_ADMIN', 'Grupo ficticio de homologacao RC-SAAS-AUT'),
    (910404, 910004, 'Administradores do cliente (homologacao)', 'HOM_ADMIN', 'Grupo ficticio de homologacao RC-SAAS-AUT')
on conflict (id) do nothing;

insert into sigov.grupo_perfil(grupo_acesso_id, perfil_acesso_id)
select p.grupo_acesso_id, p.perfil_acesso_id
from (values (910401::bigint, 50::bigint), (910402::bigint, 50::bigint), (910403::bigint, 50::bigint), (910404::bigint, 50::bigint))
     as p(grupo_acesso_id, perfil_acesso_id)
where not exists (select 1 from sigov.grupo_perfil gp
                  where gp.grupo_acesso_id = p.grupo_acesso_id and gp.perfil_acesso_id = p.perfil_acesso_id);

insert into sigov.usuario_grupo(usuario_id, grupo_acesso_id)
select u.usuario_id, u.grupo_acesso_id
from (values (910301::bigint, 910401::bigint), (910302::bigint, 910402::bigint),
             (910303::bigint, 910403::bigint), (910304::bigint, 910404::bigint))
     as u(usuario_id, grupo_acesso_id)
where not exists (select 1 from sigov.usuario_grupo ug
                  where ug.usuario_id = u.usuario_id and ug.grupo_acesso_id = u.grupo_acesso_id);

-- ----------------------------------------------------------------------------
-- 8) Verificacao (resumo determinista da situacao semeada)
-- ----------------------------------------------------------------------------
select 'SEED_OK tenant='||t.slug||' status='||t.status||' assinatura='||a.status
       ||' data_fim='||coalesce(a.data_fim::text,'NULL')||' usuarios_contratados='||coalesce(a.usuarios_contratados::text,'NULL')
       ||' modulos='||(select string_agg(x.modulo_codigo||':'||x.status, ',' order by x.modulo_codigo)
                       from sigov.tenant_modulo_contratado x where x.tenant_id = t.id and x.ativo)
from sigov.tenant t
join sigov.saas_assinatura a on a.tenant_id = t.id
where t.id in (910001, 910002, 910003, 910004)
order by t.id;

select 'SEED_OK usuario='||u.login||' ativo='||u.ativo||' grupo='||(select count(*) from sigov.usuario_grupo ug where ug.usuario_id = u.id)::text
       ||' perfil='||(select string_agg(pa.codigo_externo, ',')
                      from sigov.usuario_grupo ug
                      join sigov.grupo_perfil gp on gp.grupo_acesso_id = ug.grupo_acesso_id
                      join sigov.perfil_acesso pa on pa.id = gp.perfil_acesso_id
                      where ug.usuario_id = u.id)
from sigov.usuario u
where u.id in (910301, 910302, 910303, 910304)
order by u.id;
