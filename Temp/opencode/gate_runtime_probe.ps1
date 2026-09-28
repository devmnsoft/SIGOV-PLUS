[Console]::OutputEncoding=[Text.Encoding]::UTF8
function Probe([string]$c) { docker exec sigov-postgres psql -U postgres -d postgres -A -F ' | ' -t -c $c }
"SCHEMA_MIG_MAX=" + (Probe 'select coalesce(max(version),''<null>'') from sigov.schema_migrations')
"DOCKER_MIG_EXISTS=" + (Probe "select case when to_regclass('sigov.docker_schema_migrations') is null then 'absente' else 'presente' end")
"DOCKER_MIG_COUNT=" + (Probe "select case when to_regclass('sigov.docker_schema_migrations') is null then '<na>' else (select count(*) from sigov.docker_schema_migrations)::text end")
"REQUISICOES=" + (Probe 'select count(*) from sigov.compras_empresarial_requisicao')
"RASCUNHO_DEMO=" + (Probe "select count(*) from sigov.compras_empresarial_requisicao where id in ('d0000001-0000-4000-8000-000000000001'::uuid,'d0000001-0000-4000-8000-000000000002'::uuid,'d0000001-0000-4000-8000-000000000003'::uuid,'d0000001-0000-4000-8000-000000000004'::uuid)")
"POLITICA_ROWS=" + (Probe "select case when to_regclass('sigov.compras_empresarial_aprovacao_politica') is null then 'tabela-absente' else (select count(*) from sigov.compras_empresarial_aprovacao_politica)::text end")
"PERM_705_706=" + (Probe 'select count(*) from sigov.permissao where id in (705,706) and ativo and not is_deleted')
"PERFIL_PERM_ROWS=" + (Probe 'select count(*) from sigov.perfil_permissao where perfil_acesso_id in (9001,9002)')
"RESULTADO_CODIGO_COL=" + (Probe "select case when exists(select 1 from information_schema.columns where table_schema='sigov' and table_name='compras_empresarial_recebimento_divergencia' and column_name='resultado_codigo') then 'presente' else 'ausente' end")
"USUARIOS_1_2=" + (Probe 'select count(*) from sigov.usuario where id in (1,2)')
