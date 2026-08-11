-- SIGOV+ RC50.10 - guarda canônica de acesso administrativo local.
-- EXCLUSIVO PARA DEVELOPMENT. Idempotente, sem senha em texto puro e sem remoção física.
do $guard$
declare
    v_tenant_id bigint;
    v_entidade_id bigint;
    v_exercicio_id bigint;
    v_grupo_id bigint;
    v_perfil_id bigint;
    v_login text;
    v_email text;
    v_nome text;
    v_hash text;
    v_user_id bigint;
    v_pessoa_id bigint;
begin
    if upper(coalesce(current_setting('sigov.environment', true), 'DEVELOPMENT')) <> 'DEVELOPMENT' then
        raise exception '999_super_admin_access_guard.sql somente pode ser executado em Development';
    end if;

    insert into sigov.tenant (nome, nome_fantasia, slug, status, ambiente, ativo, is_deleted)
    values ('SIGOV Local', 'SIGOV Local', 'sigov-local', 'ATIVO', 'DEVELOPMENT', true, false)
    on conflict (slug) do update set status='ATIVO', ambiente='DEVELOPMENT', ativo=true,
        is_deleted=false, updated_at=now();
    select id into strict v_tenant_id from sigov.tenant where slug='sigov-local';

    insert into sigov.entidade (tenant_id, nome, cnpj, ativo, is_deleted)
    select v_tenant_id, 'Entidade Principal', '00000000000000', true, false
    where not exists (select 1 from sigov.entidade where tenant_id=v_tenant_id and cnpj='00000000000000');
    update sigov.entidade set nome='Entidade Principal', ativo=true, is_deleted=false, updated_at=now()
    where tenant_id=v_tenant_id and cnpj='00000000000000';
    select id into v_entidade_id from sigov.entidade where tenant_id=v_tenant_id and cnpj='00000000000000'
    order by is_deleted, ativo desc, id desc limit 1;

    insert into sigov.exercicio (tenant_id, entidade_id, ano, data_inicio, data_fim, ativo, is_deleted)
    values (v_tenant_id, v_entidade_id, extract(year from current_date)::int,
            date_trunc('year', current_date)::date,
            (date_trunc('year', current_date) + interval '1 year - 1 day')::date, true, false)
    on conflict (entidade_id, ano) do update set tenant_id=excluded.tenant_id, ativo=true,
        is_deleted=false, updated_at=now();
    select id into v_exercicio_id from sigov.exercicio
    where entidade_id=v_entidade_id and ano=extract(year from current_date)::int;

    insert into sigov.perfil_nivel (codigo,nome,descricao,nivel_hierarquico,global,tenant_admin,ativo)
    values ('ADMINISTRADOR_GERAL','Administrador Geral','Acesso administrativo geral auditado.',1000,true,false,true)
    on conflict (codigo) do update set nome=excluded.nome, ativo=true;

    insert into sigov.perfil_acesso (tenant_id,entidade_id,exercicio_id,nome,codigo_externo,ativo,is_deleted)
    select v_tenant_id,v_entidade_id,v_exercicio_id,'Administrador Geral','ADMINISTRADOR_GERAL',true,false
    where not exists (select 1 from sigov.perfil_acesso where tenant_id=v_tenant_id and codigo_externo='ADMINISTRADOR_GERAL');
    update sigov.perfil_acesso set nome='Administrador Geral', ativo=true,is_deleted=false,updated_at=now()
    where tenant_id=v_tenant_id and codigo_externo='ADMINISTRADOR_GERAL';
    select id into v_perfil_id from sigov.perfil_acesso where tenant_id=v_tenant_id
    and codigo_externo='ADMINISTRADOR_GERAL' order by is_deleted,ativo desc,id desc limit 1;

    insert into sigov.grupo_acesso (tenant_id,entidade_id,exercicio_id,nome,descricao,ativo,is_deleted)
    select v_tenant_id,v_entidade_id,v_exercicio_id,'Administradores','Acessos administrativos locais',true,false
    where not exists (select 1 from sigov.grupo_acesso where tenant_id=v_tenant_id and nome='Administradores');
    update sigov.grupo_acesso set ativo=true,is_deleted=false,updated_at=now()
    where tenant_id=v_tenant_id and nome='Administradores';
    select id into v_grupo_id from sigov.grupo_acesso where tenant_id=v_tenant_id and nome='Administradores'
    order by is_deleted,ativo desc,id desc limit 1;

    insert into sigov.grupo_perfil(grupo_acesso_id,perfil_acesso_id,is_deleted)
    values(v_grupo_id,v_perfil_id,false)
    on conflict(grupo_acesso_id,perfil_acesso_id) do update set is_deleted=false;
    insert into sigov.perfil_permissao(perfil_acesso_id,permissao_id)
    select v_perfil_id,id from sigov.permissao where ativo and not is_deleted on conflict do nothing;

    foreach v_login in array array['admin','superadmin'] loop
        if v_login='admin' then
            v_email := 'admin@sigov.local'; v_nome := 'Administrador Geral';
            v_hash := 'SIGOV_PBKDF2_V1$210000$U0lHT1ZfREVWX1NBTFQhIQ==$kKnj2QPLDyk92OudwUguJk6BJV8qHTDJTvWv+v9JLxQ=';
        else
            v_email := 'superadmin@sigov.local'; v_nome := 'Super Administrador';
            v_hash := 'SIGOV_PBKDF2_V1$210000$U0lHT1ZfU1VQRVJfU0FMVA==$55mXRMqQ4e9CW6f4f2qCvH/Ony2irtPRb4S7SjfeqFI=';
        end if;

        select id into v_user_id from sigov.usuario
        where lower(login)=v_login or lower(email)=v_email
        order by is_deleted, ativo desc, bloqueado, id desc limit 1;

        -- Libera as chaves naturais sem apagar o histórico; o registro canônico é preservado.
        update sigov.usuario set login=v_login||'_legado_'||id,
            email=v_login||'_legado_'||id||'@invalid.local', ativo=false,is_deleted=true,updated_at=now()
        where id is distinct from v_user_id and (lower(login)=v_login or lower(email)=v_email);

        select id into v_pessoa_id from sigov.pessoa
        where tenant_id=v_tenant_id and documento=v_email order by is_deleted,ativo desc,id desc limit 1;
        if v_pessoa_id is null then
            insert into sigov.pessoa(tenant_id,entidade_id,exercicio_id,tipo_pessoa,nome,documento,ativo,is_deleted)
            values(v_tenant_id,v_entidade_id,v_exercicio_id,'F',v_nome,v_email,true,false) returning id into v_pessoa_id;
        else
            update sigov.pessoa set entidade_id=v_entidade_id,exercicio_id=v_exercicio_id,nome=v_nome,
                ativo=true,is_deleted=false,updated_at=now() where id=v_pessoa_id;
        end if;

        if v_user_id is null then
            insert into sigov.usuario(tenant_id,entidade_id,exercicio_id,pessoa_id,nome,login,email,senha_hash,
                tipo_usuario,senha_deve_ser_alterada,deve_alterar_senha,bloqueado,tentativas_invalidas,
                bloqueado_ate,ativo,is_deleted)
            values(v_tenant_id,v_entidade_id,v_exercicio_id,v_pessoa_id,v_nome,v_login,v_email,v_hash,
                'ADMINISTRADOR_GERAL',false,false,false,0,null,true,false) returning id into v_user_id;
        else
            update sigov.usuario set tenant_id=v_tenant_id,entidade_id=v_entidade_id,exercicio_id=v_exercicio_id,
                pessoa_id=v_pessoa_id,nome=v_nome,login=v_login,email=v_email,senha_hash=v_hash,
                tipo_usuario='ADMINISTRADOR_GERAL',senha_deve_ser_alterada=false,deve_alterar_senha=false,
                bloqueado=false,tentativas_invalidas=0,bloqueado_ate=null,ativo=true,is_deleted=false,updated_at=now()
            where id=v_user_id;
        end if;

        insert into sigov.usuario_grupo(usuario_id,grupo_acesso_id,is_deleted)
        values(v_user_id,v_grupo_id,false) on conflict(usuario_id,grupo_acesso_id) do update set is_deleted=false;
        insert into sigov.usuario_entidade(usuario_id,entidade_id,ativo)
        values(v_user_id,v_entidade_id,true) on conflict(usuario_id,entidade_id) do update set ativo=true;
        insert into sigov.usuario_exercicio(usuario_id,exercicio_id,ativo)
        values(v_user_id,v_exercicio_id,true) on conflict(usuario_id,exercicio_id) do update set ativo=true;
        insert into sigov.usuario_escopo_acesso(tenant_id,usuario_id,entidade_id,exercicio_id,escopo,ativo)
        values(v_tenant_id,v_user_id,v_entidade_id,v_exercicio_id,'GLOBAL',true) on conflict do nothing;
    end loop;

    insert into sigov.tenant_modulo_contratado(tenant_id,modulo_codigo,status,contratado_em,vigencia_inicio,ativo)
    select v_tenant_id,codigo,'HABILITADO',current_date,current_date,true
    from sigov.modulo_saas where ativo and not is_deleted
    on conflict(tenant_id,modulo_codigo) do update set status='HABILITADO',ativo=true,updated_at=now();
end
$guard$;
