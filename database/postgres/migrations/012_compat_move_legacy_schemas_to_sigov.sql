do $$
declare
    legacy_schema text;
    legacy_table text;
    legacy_schemas text[] := array['core','sec','audit','lgpd','workflow','bi','fin','trib','compras','rh','educ','saude','social','san','geo','suporte','integracao','config'];
    known_tables text[] := array['pessoa','pessoa_fisica','pessoa_juridica','endereco','contato','entidade','exercicio','unidade_organizacional','usuario','grupo_acesso','perfil_acesso','permissao','trilha_auditoria','log_aplicacao','log_erro','acesso_dado_pessoal','consentimento','solicitacao_titular','chamado','chamado_interacao','schema_migrations'];
    remaining_count integer;
begin
    create schema if not exists sigov;

    foreach legacy_schema in array legacy_schemas loop
        if exists (select 1 from information_schema.schemata where schema_name = legacy_schema) then
            foreach legacy_table in array known_tables loop
                if exists (select 1 from information_schema.tables t where t.table_schema = legacy_schema and t.table_name = legacy_table)
                   and not exists (select 1 from information_schema.tables t where t.table_schema = 'sigov' and t.table_name = legacy_table) then
                    execute format('alter table %I.%I set schema sigov', legacy_schema, legacy_table);
                end if;
            end loop;

            select count(1) into remaining_count
            from information_schema.tables t
            where t.table_schema = legacy_schema;

            if remaining_count = 0 then
                execute format('drop schema if exists %I', legacy_schema);
            end if;
        end if;
    end loop;
end $$;
