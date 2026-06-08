do $$
declare
    v_entidade_id bigint;
    v_exercicio_id bigint;
    v_pessoa_id bigint;
    v_usuario_id bigint;
    v_perfil_id bigint;
    v_admin_password text := coalesce(nullif(current_setting('sigov.admin_password', true), ''), 'SigovDevLocal!2026');
begin
    insert into sigov.entidade (nome, cnpj, ativo, observacao)
    values ('Prefeitura Municipal de Demonstração', '00000000000191', true, 'Registro de desenvolvimento sigov')
    on conflict do nothing;

    select id into v_entidade_id from sigov.entidade where cnpj = '00000000000191' and is_deleted = false limit 1;

    insert into sigov.exercicio (entidade_id, ano, data_inicio, data_fim, ativo)
    values (v_entidade_id, extract(year from now())::integer, make_date(extract(year from now())::integer, 1, 1), make_date(extract(year from now())::integer, 12, 31), true)
    on conflict do nothing;

    select id into v_exercicio_id from sigov.exercicio where entidade_id = v_entidade_id and ano = extract(year from now())::integer limit 1;

    insert into sigov.pessoa (entidade_id, exercicio_id, nome, tipo_pessoa, documento, ativo)
    values (v_entidade_id, v_exercicio_id, 'Administrador do Sistema', 'F', '00000000191', true)
    on conflict do nothing;

    select id into v_pessoa_id from sigov.pessoa where documento = '00000000191' and is_deleted = false limit 1;

    insert into sigov.pessoa_fisica (entidade_id, exercicio_id, pessoa_id, cpf, ativo)
    values (v_entidade_id, v_exercicio_id, v_pessoa_id, '00000000191', true)
    on conflict do nothing;

    insert into sigov.usuario (entidade_id, exercicio_id, pessoa_id, login, email, senha_hash, ativo, observacao)
    values (v_entidade_id, v_exercicio_id, v_pessoa_id, 'admin', 'admin@sigov.local', 'DEV_ONLY:' || v_admin_password, true, 'Senha inicial definida por SIGOV_ADMIN_PASSWORD; fallback Development SigovDevLocal!2026')
    on conflict do nothing;

    select id into v_usuario_id from sigov.usuario where login = 'admin' and is_deleted = false limit 1;

    insert into sigov.perfil_acesso (entidade_id, exercicio_id, nome, descricao, ativo)
    values (v_entidade_id, v_exercicio_id, 'Administrador', 'Perfil administrativo de desenvolvimento', true)
    on conflict do nothing;

    select id into v_perfil_id from sigov.perfil_acesso where nome = 'Administrador' and is_deleted = false limit 1;

    insert into sigov.permissao (entidade_id, exercicio_id, modulo, chave, descricao, ativo)
    values
        (v_entidade_id, v_exercicio_id, 'core', 'core_admin', 'Permissão administrativa do núcleo', true),
        (v_entidade_id, v_exercicio_id, 'seguranca', 'seguranca_admin', 'Permissão administrativa de segurança', true),
        (v_entidade_id, v_exercicio_id, 'auditoria', 'auditoria_admin', 'Permissão administrativa de auditoria', true),
        (v_entidade_id, v_exercicio_id, 'lgpd', 'lgpd_admin', 'Permissão administrativa LGPD', true),
        (v_entidade_id, v_exercicio_id, 'suporte', 'suporte_admin', 'Permissão administrativa de suporte', true),
        (v_entidade_id, v_exercicio_id, 'conformidade', 'aderencia_admin', 'Permissão administrativa de conformidade e aderência', true)
    on conflict do nothing;

    insert into sigov.perfil_permissao (perfil_acesso_id, permissao_id)
    select v_perfil_id, p.id
    from sigov.permissao p
    where p.modulo in ('core', 'seguranca', 'auditoria', 'lgpd', 'suporte', 'conformidade')
    on conflict do nothing;

    insert into sigov.grupo_acesso (entidade_id, exercicio_id, nome, descricao, ativo)
    values (v_entidade_id, v_exercicio_id, 'Administradores', 'Grupo administrativo de desenvolvimento', true)
    on conflict do nothing;

    insert into sigov.trilha_auditoria (entidade_id, exercicio_id, usuario_id, tabela, registro_id, acao, valores_novos, observacao)
    values (v_entidade_id, v_exercicio_id, v_usuario_id, 'sigov.usuario', v_usuario_id::varchar, 'SEED', jsonb_build_object('login', 'admin'), 'Seed inicial sigov')
    on conflict do nothing;
end $$;

-- Camadas estruturais de desenvolvimento do módulo Agro; não executar em Production.
insert into sigov.agro_geo_camada (tenant_id, entidade_id, codigo, nome, tipo_camada, descricao, publica, created_by)
select t.id, e.id, v.codigo, v.nome, v.codigo, 'Camada estrutural Agro criada apenas para Development.', false, null
  from sigov.tenant t
  left join sigov.entidade e on e.tenant_id = t.id and e.is_deleted = false
 cross join (values
    ('PRODUTORES','Produtores rurais'),
    ('PROPRIEDADES','Propriedades rurais'),
    ('TALHOES','Talhões'),
    ('ESTRADAS','Estradas vicinais'),
    ('PONTOS_CRITICOS','Pontos críticos'),
    ('FEIRAS','Feiras'),
    ('AGROINDUSTRIAS','Agroindústrias')
 ) as v(codigo, nome)
 where coalesce(t.ambiente, 'DEVELOPMENT') <> 'PRODUCTION'
on conflict do nothing;
