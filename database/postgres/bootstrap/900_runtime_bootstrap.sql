-- SIGOV+ RC38E
-- Bootstrap operacional idempotente executado somente pelo instalador one-shot.
-- Os placeholders __SIGOV_*__ são substituídos pelo instalador antes da execução.
-- Não inserir senha em texto puro: __SIGOV_ADMIN_PASSWORD_HASH__ já deve chegar no
-- formato SIGOV_PBKDF2_V1 aceito por PasswordHashService.

do $$
declare
    v_tenant_id bigint;
    v_entidade_id bigint;
    v_exercicio_id bigint;
    v_pessoa_id bigint;
    v_usuario_id bigint;
    v_grupo_id bigint;
    v_perfil_id bigint;
    v_plano_id bigint;
    v_ano integer := __SIGOV_CURRENT_YEAR__;
begin
    insert into sigov.tenant (
        nome, nome_fantasia, documento, slug, status, timezone, locale, ambiente,
        data_inicio_operacao, metadados, ativo, is_deleted
    )
    values (
        '__SIGOV_TENANT_NAME__', '__SIGOV_TENANT_NAME__', nullif('__SIGOV_TENANT_DOCUMENT__', ''),
        '__SIGOV_TENANT_SLUG__', 'ATIVO', 'America/Sao_Paulo', 'pt-BR',
        '__SIGOV_ENVIRONMENT__', now(),
        jsonb_build_object('bootstrap', 'RC38E', 'oneShot', true), true, false
    )
    on conflict (slug) do update set
        nome = excluded.nome,
        nome_fantasia = excluded.nome_fantasia,
        documento = coalesce(excluded.documento, sigov.tenant.documento),
        status = 'ATIVO',
        ativo = true,
        is_deleted = false,
        updated_at = now()
    returning id into v_tenant_id;

    select id into v_entidade_id
      from sigov.entidade
     where tenant_id = v_tenant_id
       and cnpj = '__SIGOV_ENTITY_CNPJ__'
       and is_deleted = false
     order by id
     limit 1;

    if v_entidade_id is null then
        insert into sigov.entidade (tenant_id, nome, cnpj, ativo, is_deleted, observacao)
        values (v_tenant_id, '__SIGOV_ENTITY_NAME__', '__SIGOV_ENTITY_CNPJ__', true, false, 'Entidade criada pelo instalador one-shot RC38E.')
        returning id into v_entidade_id;
    else
        update sigov.entidade
           set nome = '__SIGOV_ENTITY_NAME__', ativo = true, is_deleted = false, updated_at = now()
         where id = v_entidade_id;
    end if;

    insert into sigov.exercicio (tenant_id, entidade_id, ano, data_inicio, data_fim, ativo, is_deleted)
    values (v_tenant_id, v_entidade_id, v_ano, make_date(v_ano, 1, 1), make_date(v_ano, 12, 31), true, false)
    on conflict (entidade_id, ano) do update set
        tenant_id = excluded.tenant_id,
        data_inicio = excluded.data_inicio,
        data_fim = excluded.data_fim,
        ativo = true,
        is_deleted = false,
        updated_at = now()
    returning id into v_exercicio_id;

    select id into v_pessoa_id
      from sigov.pessoa
     where tenant_id = v_tenant_id
       and lower(coalesce(documento, '')) = lower('__SIGOV_ADMIN_EMAIL__')
       and is_deleted = false
     order by id
     limit 1;

    if v_pessoa_id is null then
        insert into sigov.pessoa (
            tenant_id, entidade_id, exercicio_id, tipo_pessoa, nome, documento,
            classificacao_lgpd, ativo, is_deleted, observacao
        )
        values (
            v_tenant_id, v_entidade_id, v_exercicio_id, 'F', '__SIGOV_ADMIN_NAME__',
            '__SIGOV_ADMIN_EMAIL__', 'DADO_PESSOAL', true, false,
            'Administrador inicial criado pelo instalador one-shot RC38E.'
        )
        returning id into v_pessoa_id;
    else
        update sigov.pessoa
           set entidade_id = v_entidade_id,
               exercicio_id = v_exercicio_id,
               nome = '__SIGOV_ADMIN_NAME__',
               ativo = true,
               is_deleted = false,
               updated_at = now()
         where id = v_pessoa_id;
    end if;

    select id into v_usuario_id
      from sigov.usuario
     where tenant_id = v_tenant_id
       and (lower(login) = lower('__SIGOV_ADMIN_LOGIN__') or lower(email) = lower('__SIGOV_ADMIN_EMAIL__'))
       and is_deleted = false
     order by id
     limit 1;

    if v_usuario_id is null then
        insert into sigov.usuario (
            tenant_id, entidade_id, exercicio_id, pessoa_id, nome, login, email,
            senha_hash, tipo_usuario, senha_deve_ser_alterada, deve_alterar_senha,
            bloqueado, tentativas_invalidas, ativo, is_deleted, observacao
        )
        values (
            v_tenant_id, v_entidade_id, v_exercicio_id, v_pessoa_id,
            '__SIGOV_ADMIN_NAME__', '__SIGOV_ADMIN_LOGIN__', '__SIGOV_ADMIN_EMAIL__',
            '__SIGOV_ADMIN_PASSWORD_HASH__', 'ADMINISTRADOR_GERAL', true, true,
            false, 0, true, false,
            'Senha temporária gerada pelo instalador. Alteração obrigatória no primeiro acesso.'
        )
        returning id into v_usuario_id;
    else
        update sigov.usuario
           set entidade_id = v_entidade_id,
               exercicio_id = v_exercicio_id,
               pessoa_id = coalesce(pessoa_id, v_pessoa_id),
               nome = '__SIGOV_ADMIN_NAME__',
               email = '__SIGOV_ADMIN_EMAIL__',
               senha_hash = '__SIGOV_ADMIN_PASSWORD_HASH__',
               tipo_usuario = 'ADMINISTRADOR_GERAL',
               senha_deve_ser_alterada = true,
               deve_alterar_senha = true,
               bloqueado = false,
               tentativas_invalidas = 0,
               bloqueado_ate = null,
               ativo = true,
               is_deleted = false,
               updated_at = now()
         where id = v_usuario_id;
    end if;

    insert into sigov.perfil_acesso (
        tenant_id, entidade_id, exercicio_id, nome, descricao, codigo_externo, ativo, is_deleted
    )
    select v_tenant_id, v_entidade_id, v_exercicio_id,
           'Administrador Geral',
           'Acesso administrativo completo ao tenant criado pelo instalador.',
           'ADMINISTRADOR_GERAL', true, false
    where not exists (
        select 1 from sigov.perfil_acesso
         where tenant_id = v_tenant_id
           and codigo_externo = 'ADMINISTRADOR_GERAL'
           and is_deleted = false
    );

    select id into v_perfil_id
      from sigov.perfil_acesso
     where tenant_id = v_tenant_id
       and codigo_externo = 'ADMINISTRADOR_GERAL'
       and is_deleted = false
     order by id
     limit 1;

    insert into sigov.grupo_acesso (
        tenant_id, entidade_id, exercicio_id, nome, descricao, ativo, is_deleted
    )
    select v_tenant_id, v_entidade_id, v_exercicio_id,
           'Administradores', 'Grupo administrativo inicial do tenant.', true, false
    where not exists (
        select 1 from sigov.grupo_acesso
         where tenant_id = v_tenant_id
           and nome = 'Administradores'
           and is_deleted = false
    );

    select id into v_grupo_id
      from sigov.grupo_acesso
     where tenant_id = v_tenant_id
       and nome = 'Administradores'
       and is_deleted = false
     order by id
     limit 1;

    insert into sigov.usuario_grupo (tenant_id, usuario_id, grupo_acesso_id, is_deleted)
    values (v_tenant_id, v_usuario_id, v_grupo_id, false)
    on conflict (usuario_id, grupo_acesso_id) do update set
        tenant_id = excluded.tenant_id,
        is_deleted = false;

    insert into sigov.grupo_perfil (tenant_id, grupo_acesso_id, perfil_acesso_id, is_deleted)
    values (v_tenant_id, v_grupo_id, v_perfil_id, false)
    on conflict (grupo_acesso_id, perfil_acesso_id) do update set
        tenant_id = excluded.tenant_id,
        is_deleted = false;

    insert into sigov.perfil_permissao (tenant_id, perfil_acesso_id, permissao_id)
    select v_tenant_id, v_perfil_id, p.id
      from sigov.permissao p
     where p.ativo = true and p.is_deleted = false
    on conflict (perfil_acesso_id, permissao_id) do update set tenant_id = excluded.tenant_id;

    insert into sigov.usuario_entidade (tenant_id, usuario_id, entidade_id, ativo)
    values (v_tenant_id, v_usuario_id, v_entidade_id, true)
    on conflict (usuario_id, entidade_id) do update set tenant_id = excluded.tenant_id, ativo = true;

    insert into sigov.usuario_exercicio (tenant_id, usuario_id, exercicio_id, ativo)
    values (v_tenant_id, v_usuario_id, v_exercicio_id, true)
    on conflict (usuario_id, exercicio_id) do update set tenant_id = excluded.tenant_id, ativo = true;

    insert into sigov.usuario_escopo_acesso (
        tenant_id, usuario_id, entidade_id, exercicio_id, modulo_codigo, escopo, ativo
    )
    select v_tenant_id, v_usuario_id, null, null, null, 'TENANT', true
    where not exists (
        select 1 from sigov.usuario_escopo_acesso
         where tenant_id = v_tenant_id and usuario_id = v_usuario_id
           and escopo = 'TENANT' and ativo = true
    );

    insert into sigov.plano_saas (codigo, nome, descricao, ativo, is_deleted)
    values ('COMPLETO', 'Plano Completo', 'Todos os módulos disponíveis para a instalação inicial.', true, false)
    on conflict (codigo) do update set nome = excluded.nome, descricao = excluded.descricao, ativo = true, is_deleted = false
    returning id into v_plano_id;

    insert into sigov.plano_modulo (plano_saas_id, modulo_saas_id, ativo, is_deleted)
    select v_plano_id, m.id, true, false
      from sigov.modulo_saas m
     where m.ativo = true and m.is_deleted = false
    on conflict (plano_saas_id, modulo_saas_id) do update set ativo = true, is_deleted = false;

    if not exists (
        select 1 from sigov.tenant_assinatura
         where tenant_id = v_tenant_id and ativo = true and is_deleted = false
    ) then
        insert into sigov.tenant_assinatura (
            tenant_id, plano_saas_id, status, inicio_at, ativo, is_deleted, metadados
        )
        values (
            v_tenant_id, v_plano_id, 'ATIVA', now(), true, false,
            jsonb_build_object('bootstrap', 'RC38E')
        );
    else
        update sigov.tenant_assinatura
           set plano_saas_id = v_plano_id,
               status = 'ATIVA',
               ativo = true,
               is_deleted = false,
               updated_at = now()
         where tenant_id = v_tenant_id and ativo = true and is_deleted = false;
    end if;

    insert into sigov.tenant_modulo (
        tenant_id, modulo_saas_id, habilitado, contratado, inicio_at, configuracoes, ativo, is_deleted
    )
    select v_tenant_id, m.id, true, true, now(), '{}'::jsonb, true, false
      from sigov.modulo_saas m
     where m.ativo = true and m.is_deleted = false
    on conflict (tenant_id, modulo_saas_id) do update set
        habilitado = true,
        contratado = true,
        ativo = true,
        is_deleted = false,
        fim_at = null,
        updated_at = now();

    insert into sigov.tenant_modulo_contratado (
        tenant_id, modulo_codigo, pacote_codigo, status, contratado_em,
        vigencia_inicio, parametros_json, ativo
    )
    select v_tenant_id, m.codigo, 'COMPLETO', 'HABILITADO', current_date,
           current_date, '{}'::jsonb, true
      from sigov.modulo_saas m
     where m.ativo = true and m.is_deleted = false
    on conflict (tenant_id, modulo_codigo) do update set
        pacote_codigo = 'COMPLETO',
        status = 'HABILITADO',
        vigencia_fim = null,
        ativo = true,
        updated_at = now();

    insert into sigov.tenant_configuracao (tenant_id, chave, valor, secreto, ativo, is_deleted)
    values
        (v_tenant_id, 'sistema.locale', '"pt-BR"'::jsonb, false, true, false),
        (v_tenant_id, 'sistema.timezone', '"America/Sao_Paulo"'::jsonb, false, true, false),
        (v_tenant_id, 'sistema.moeda', '"BRL"'::jsonb, false, true, false),
        (v_tenant_id, 'sistema.bootstrap_concluido', 'true'::jsonb, false, true, false),
        (v_tenant_id, 'seguranca.exigir_troca_senha_inicial', 'true'::jsonb, false, true, false)
    on conflict (tenant_id, chave) do update set
        valor = excluded.valor,
        secreto = excluded.secreto,
        ativo = true,
        is_deleted = false,
        updated_at = now();

    insert into sigov.tenant_parametro_valor (
        tenant_id, entidade_id, exercicio_id, usuario_id, modulo_codigo,
        escopo, parametro_definicao_id, valor, ativo
    )
    select v_tenant_id, null, null, null, d.modulo,
           'TENANT', d.id, coalesce(d.valor_padrao, '{}'::jsonb), true
      from sigov.tenant_parametro_definicao d
     where d.ativo = true
       and not exists (
           select 1 from sigov.tenant_parametro_valor pv
            where pv.tenant_id = v_tenant_id
              and pv.parametro_definicao_id = d.id
              and pv.escopo = 'TENANT'
              and pv.entidade_id is null
              and pv.exercicio_id is null
              and pv.usuario_id is null
              and pv.ativo = true
       );

    insert into sigov.tenant_feature_flag (
        tenant_id, feature_flag_def_id, modulo_codigo, feature_codigo,
        habilitado, habilitada, valor, parametros_json, ativo, is_deleted
    )
    select v_tenant_id, f.id, coalesce(f.modulo, split_part(f.codigo, '.', 1)), f.codigo,
           true, true, '{}'::jsonb, '{}'::jsonb, true, false
      from sigov.feature_flag_def f
     where f.ativo = true and f.is_deleted = false
    on conflict (tenant_id, feature_flag_def_id) do update set
        modulo_codigo = excluded.modulo_codigo,
        feature_codigo = excluded.feature_codigo,
        habilitado = true,
        habilitada = true,
        ativo = true,
        is_deleted = false,
        updated_at = now();

    insert into sigov.politica_senha (
        tenant_id, entidade_id, exercicio_id, tamanho_minimo, exigir_mfa,
        validade_dias, ativo, is_deleted, observacao
    )
    select v_tenant_id, v_entidade_id, v_exercicio_id, 10, false, 90, true, false,
           'Política inicial criada pelo instalador RC38E.'
    where not exists (
        select 1 from sigov.politica_senha
         where tenant_id = v_tenant_id and ativo = true and is_deleted = false
    );

    insert into sigov.auditoria_evento (
        tenant_id, usuario_id, acao, entidade, entidade_id, depois, correlation_id
    )
    values (
        v_tenant_id, v_usuario_id, 'BOOTSTRAP_ONE_SHOT_CONCLUIDO', 'sigov.tenant',
        v_tenant_id::varchar,
        jsonb_build_object(
            'tenantSlug', '__SIGOV_TENANT_SLUG__',
            'entidadeId', v_entidade_id,
            'exercicioId', v_exercicio_id,
            'usuarioId', v_usuario_id,
            'versaoBootstrap', 'RC38E'
        ),
        gen_random_uuid()
    );
end $$;
