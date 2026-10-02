do $$
declare
    v_table_name text;
    operational_tables text[] := array[
        'entidade','exercicio','unidade_organizacional','pessoa','pessoa_fisica','pessoa_juridica','endereco','contato',
        'usuario','grupo_acesso','usuario_grupo','perfil_acesso','perfil_permissao','sessao_usuario','historico_login',
        'trilha_auditoria','log_aplicacao','log_erro','fila_evento','acesso_dado_pessoal','consentimento','solicitacao_titular',
        'relatorio_titular','incidente_seguranca','chamado','chamado_interacao','satisfacao_atendimento','api_credential',
        'notificacao','tarefa','agenda_obrigacao','controle_sequencial','usuario_entidade','usuario_exercicio','grupo_perfil',
        'validacao_requisito','validacao_resultado','termo_aceite','integracao_sistema','webhook_recebido','camada','geolocalizacao'
    ];
    default_tenant_id bigint;
begin
    insert into sigov.tenant (nome, nome_fantasia, slug, status, ambiente, data_inicio_operacao)
    values ('Tenant de Desenvolvimento sigov', 'sigov Development', 'municipio-demo', 'ATIVO', 'DEVELOPMENT', now())
    on conflict (slug) do nothing;

    select id into default_tenant_id from sigov.tenant where slug = 'municipio-demo';

    foreach v_table_name in array operational_tables loop
        if exists (select 1 from information_schema.tables where table_schema = 'sigov' and table_name = v_table_name) then
            if not exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = v_table_name and column_name = 'tenant_id') then
                execute format('alter table sigov.%I add column tenant_id bigint null', v_table_name);
            end if;

            execute format('update sigov.%I set tenant_id = $1 where tenant_id is null', v_table_name) using default_tenant_id;
            execute format('alter table sigov.%I alter column tenant_id set not null', v_table_name);
            execute format('create index if not exists idx_%I_tenant_id on sigov.%I (tenant_id)', v_table_name, v_table_name);

            if exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = v_table_name and column_name = 'entidade_id') then
                execute format('create index if not exists idx_%I_tenant_entidade on sigov.%I (tenant_id, entidade_id)', v_table_name, v_table_name);
            end if;

            if exists (select 1 from information_schema.columns where table_schema = 'sigov' and table_name = v_table_name and column_name = 'exercicio_id') then
                execute format('create index if not exists idx_%I_tenant_exercicio on sigov.%I (tenant_id, exercicio_id)', v_table_name, v_table_name);
            end if;
        end if;
    end loop;
end $$;

alter table sigov.usuario add column if not exists tipo_usuario varchar(30) not null default 'TENANT_USER';
alter table sigov.api_credential add column if not exists key_hash varchar(200) null;
alter table sigov.fila_evento add column if not exists proxima_tentativa_at timestamptz null;
alter table sigov.fila_evento add column if not exists erro text null;
alter table sigov.chamado add column if not exists numero varchar(60) null;

create index if not exists idx_usuario_tenant_login on sigov.usuario (tenant_id, login);
create index if not exists idx_usuario_tenant_email on sigov.usuario (tenant_id, email);
create index if not exists idx_pessoa_tenant_documento on sigov.pessoa (tenant_id, documento);
create index if not exists idx_chamado_tenant_numero on sigov.chamado (tenant_id, numero);
create index if not exists idx_trilha_auditoria_tenant_created_at on sigov.trilha_auditoria (tenant_id, created_at desc);
create index if not exists idx_solicitacao_titular_tenant_status on sigov.solicitacao_titular (tenant_id, status);
