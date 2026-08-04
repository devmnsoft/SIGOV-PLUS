do $$ begin
  if current_setting('server_version_num')::integer < 160000 then raise exception 'PostgreSQL 16+ é obrigatório'; end if;
  if to_regnamespace('sigov') is null then raise exception 'Schema sigov ausente'; end if;
  if to_regclass('sigov.schema_migrations') is null or to_regclass('sigov.entidade') is null or to_regclass('sigov.exercicio') is null then raise exception 'Núcleo SIGOV+ incompleto'; end if;
  if not exists(select 1 from sigov.entidade where ativo and not is_deleted) then raise exception 'Nenhuma entidade operacional'; end if;
  if not exists(select 1 from sigov.exercicio where ativo and not is_deleted) then raise exception 'Nenhum exercício operacional'; end if;
end $$;
select 'core_runtime_ok';
