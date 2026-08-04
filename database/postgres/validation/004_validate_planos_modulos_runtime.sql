do $$ begin
  if to_regclass('sigov.plano_saas') is null or to_regclass('sigov.modulo_saas') is null or to_regclass('sigov.tenant_modulo_contratado') is null then raise exception 'Modelo de planos/módulos incompleto'; end if;
  if not exists(select 1 from sigov.modulo_saas where ativo and not is_deleted) then raise exception 'Catálogo de módulos vazio'; end if;
  if exists(select 1 from sigov.tenant t where t.ativo and not t.is_deleted and not exists(select 1 from sigov.tenant_modulo_contratado m where m.tenant_id=t.id and m.ativo)) then raise exception 'Tenant ativo sem módulo contratado'; end if;
end $$;
select 'planos_modulos_runtime_ok';
