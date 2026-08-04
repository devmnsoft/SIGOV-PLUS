do $$ begin
  if to_regclass('sigov.tenant') is null then raise exception 'Tabela tenant ausente'; end if;
  if not exists(select 1 from sigov.tenant where ativo and not is_deleted) then raise exception 'Nenhum tenant ativo'; end if;
  if exists(select slug from sigov.tenant where not is_deleted group by slug having count(*)>1) then raise exception 'Slug de tenant duplicado'; end if;
  if exists(select 1 from sigov.entidade e left join sigov.tenant t on t.id=e.tenant_id where not e.is_deleted and t.id is null) then raise exception 'Entidade órfã de tenant'; end if;
end $$;
select 'tenant_runtime_ok';
