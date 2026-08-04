do $$ begin
  if to_regclass('sigov.tenant_configuracao') is null or to_regclass('sigov.tenant_feature_flag') is null then raise exception 'Parâmetros/feature flags incompletos'; end if;
  if exists(select 1 from sigov.tenant_feature_flag where tenant_id is null) then raise exception 'Feature flag sem tenant'; end if;
  if exists(select 1 from sigov.tenant t where t.ativo and not t.is_deleted and not exists(select 1 from sigov.tenant_configuracao c where c.tenant_id=t.id and c.chave='sistema.bootstrap_concluido' and c.ativo and not c.is_deleted)) then raise exception 'Tenant sem parâmetro de bootstrap'; end if;
end $$;
select 'parametros_featureflags_runtime_ok';
