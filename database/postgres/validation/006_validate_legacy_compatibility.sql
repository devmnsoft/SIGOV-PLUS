do $$ declare missing text; begin
  select string_agg(v.t||'.'||v.c,', ') into missing from (values ('usuario','tenant_id'),('usuario','senha_deve_ser_alterada'),('permissao','recurso'),('permissao','acao'),('tenant_feature_flag','feature_codigo'),('tenant_feature_flag','habilitada'))v(t,c) where not exists(select 1 from information_schema.columns x where x.table_schema='sigov' and x.table_name=v.t and x.column_name=v.c);
  if missing is not null then raise exception 'Colunas de compatibilidade ausentes: %',missing; end if;
  if to_regclass('sigov.compras_fornecedor') is null then raise exception 'Compatibilidade de compras ausente'; end if;
end $$;
select 'legacy_compatibility_ok';
