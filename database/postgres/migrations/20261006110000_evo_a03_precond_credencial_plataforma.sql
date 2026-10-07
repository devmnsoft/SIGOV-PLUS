-- ============================================================
-- RC-EVO (Bloco A) | 20261006110000 | A03
-- Precondicao das grants de escopo plataforma (RC-SAAS-AUT
-- aut_05, 20261006120000) em instalacao limpa.
--
-- Raiz do defeito (baseline 2026-10-06, banco descartavel):
--   Com A02 aplicada, a instalacao limpa passou da aut_01 e parou
--   na aut_05 com "nenhum usuario ativo com login
--   admin/superadmin". Nenhuma migration cria usuarios: as
--   credenciais so existem DEPOIS das migrations, criadas pelo
--   instalador one-shot 900_runtime_bootstrap.sql (login
--   parametrizado, padrao 'admin', fora dos scripts consolidados)
--   ou pelo seed development
--   999_super_admin_access_guard.sql (guard exclusivo de
--   Development, tambem fora dos scripts de baseline). Em bancos
--   legados/upgrade os usuarios ja existem, por isso a falha so
--   aparecia em instalacao limpa (mascarada ate agora pela parada
--   anterior da aut_01/A02).
--   O alvo exige duas credenciais: a pos-condicao publicada da
--   aut_05 requer >= 10 pares (usuario x chave saas) na cadeia
--   plataforma, o que pressupoe 'admin' E 'superadmin' ativos.
--
-- Correcao (regra 8: migration nova; nenhuma publicada e
-- alterada): posicionada entre 20261005120000 (aut_04) e
-- 20261006120000 (aut_05) no manifest. Semantica:
--   * ambiente legado (ja existe admin/superadmin ativo):
--     NAO FAZ NADA;
--   * banco novo: cria o tenant de plataforma 'plataforma-mnsoft'
--     com entidade, exercicio, pessoas ficticias e os usuarios
--     'admin' e 'superadmin' (hashes PBKDF2 documentados no seed
--     999; alteracao obrigatoria no primeiro acesso). O instalador
--     one-shot (900) adota o 'admin' para o tenant cliente em
--     execucao posterior (logins sao unicos globais, indice
--     idx_usuario_login); o seed 999 adota ambos em Development.
--   * dupla passada do instalador (idempotencia): segunda execucao
--     eh no-op pelo proprio guard.
-- Regras 9/10/13: somente dados ficticios, sem segredo real,
-- falha explicita via raise interno e postConditionSql no
-- manifest.
-- ============================================================

do $a03$
declare
    v_tenant_id bigint;
    v_entidade_id bigint;
    v_exercicio_id bigint;
    v_pessoa_id bigint;
    v_user_id bigint;
    v_ano int;
    v_login text;
    v_email text;
    v_nome text;
    v_documento text;
    v_hash text;
begin
    -- Ambiente legado (ou bootstrap previo): nao faz nada.
    if exists (select 1 from sigov.usuario
               where lower(login) in ('admin', 'superadmin')
                 and ativo and not is_deleted) then
        return;
    end if;

    v_ano := extract(year from current_date)::int;

    insert into sigov.tenant (nome, nome_fantasia, slug, status, ambiente, metadados, ativo, is_deleted)
    values ('Plataforma MNSOFT', 'Plataforma MNSOFT', 'plataforma-mnsoft', 'ATIVO',
            upper(coalesce(nullif(current_setting('sigov.environment', true), ''), 'PRODUCTION')),
            '{"bootstrap":"RC-EVO-A03","plataforma":true}'::jsonb, true, false)
    on conflict (slug) do update
        set status = 'ATIVO', ativo = true, is_deleted = false, updated_at = now();
    select id into strict v_tenant_id from sigov.tenant where slug = 'plataforma-mnsoft';

    insert into sigov.entidade (tenant_id, nome, cnpj, ativo, is_deleted)
    select v_tenant_id, 'Plataforma MNSOFT', '00000000000000', true, false
    where not exists (select 1 from sigov.entidade where tenant_id = v_tenant_id and cnpj = '00000000000000');
    select id into v_entidade_id from sigov.entidade
    where tenant_id = v_tenant_id and cnpj = '00000000000000'
    order by is_deleted, ativo desc, id desc limit 1;
    if v_entidade_id is null then
        raise exception 'RC-EVO A03: entidade de plataforma nao ficou disponivel';
    end if;

    insert into sigov.exercicio (tenant_id, entidade_id, ano, data_inicio, data_fim, ativo, is_deleted)
    values (v_tenant_id, v_entidade_id, v_ano, make_date(v_ano, 1, 1), make_date(v_ano, 12, 31), true, false)
    on conflict (entidade_id, ano) do update
        set tenant_id = excluded.tenant_id, ativo = true, is_deleted = false, updated_at = now();
    select id into v_exercicio_id from sigov.exercicio where entidade_id = v_entidade_id and ano = v_ano;
    if v_exercicio_id is null then
        raise exception 'RC-EVO A03: exercicio de plataforma nao ficou disponivel';
    end if;

    foreach v_login in array array['admin', 'superadmin'] loop
        if v_login = 'admin' then
            v_email := 'admin@sigov.local';
            v_nome := 'Administrador Geral';
            v_documento := '00000000000001';
            v_hash := 'SIGOV_PBKDF2_V1$210000$U0lHT1ZfREVWX1NBTFQhIQ==$kKnj2QPLDyk92OudwUguJk6BJV8qHTDJTvWv+v9JLxQ=';
        else
            v_email := 'superadmin@sigov.local';
            v_nome := 'Super Administrador';
            v_documento := '00000000000002';
            v_hash := 'SIGOV_PBKDF2_V1$210000$U0lHT1ZfU1VQRVJfU0FMVA==$55mXRMqQ4e9CW6f4f2qCvH/Ony2irtPRb4S7SjfeqFI=';
        end if;

        select id into v_pessoa_id from sigov.pessoa
        where tenant_id = v_tenant_id and documento = v_documento
        order by is_deleted, ativo desc, id desc limit 1;
        if v_pessoa_id is null then
            insert into sigov.pessoa (tenant_id, entidade_id, exercicio_id, tipo_pessoa, nome, documento, ativo, is_deleted)
            values (v_tenant_id, v_entidade_id, v_exercicio_id, 'F', v_nome, v_documento, true, false)
            returning id into v_pessoa_id;
        else
            update sigov.pessoa
               set entidade_id = v_entidade_id, exercicio_id = v_exercicio_id, nome = v_nome,
                   ativo = true, is_deleted = false, updated_at = now()
             where id = v_pessoa_id;
        end if;

        insert into sigov.usuario (tenant_id, entidade_id, exercicio_id, pessoa_id, nome, login, email, senha_hash,
                                   tipo_usuario, senha_deve_ser_alterada, deve_alterar_senha, bloqueado, tentativas_invalidas,
                                   bloqueado_ate, ativo, is_deleted, observacao)
        values (v_tenant_id, v_entidade_id, v_exercicio_id, v_pessoa_id, v_nome, v_login, v_email, v_hash,
                'ADMINISTRADOR_GERAL', true, true, false, 0, null, true, false,
                'Credencial de plataforma criada por RC-EVO A03 em instalacao limpa. Alteracao obrigatoria no primeiro acesso.')
        returning id into v_user_id;

        insert into sigov.usuario_entidade (usuario_id, entidade_id, ativo)
        values (v_user_id, v_entidade_id, true)
        on conflict (usuario_id, entidade_id) do update set ativo = true;
        insert into sigov.usuario_exercicio (usuario_id, exercicio_id, ativo)
        values (v_user_id, v_exercicio_id, true)
        on conflict (usuario_id, exercicio_id) do update set ativo = true;
        insert into sigov.usuario_escopo_acesso (tenant_id, usuario_id, entidade_id, exercicio_id, modulo_codigo, escopo, ativo)
        values (v_tenant_id, v_user_id, null, null, null, 'TENANT', true)
        on conflict do nothing;
    end loop;

    -- Autoverificacao (falha explicita; regra 13).
    if (select count(*) from sigov.usuario
        where lower(login) in ('admin', 'superadmin') and ativo and not is_deleted) < 2 then
        raise exception 'RC-EVO A03: credenciais de plataforma nao ficaram ativas';
    end if;
end
$a03$;
