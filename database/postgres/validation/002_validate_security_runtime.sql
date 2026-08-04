do $$ begin
  if to_regclass('sigov.usuario') is null or to_regclass('sigov.permissao') is null then raise exception 'Modelo de segurança incompleto'; end if;
  if exists(select 1 from sigov.usuario where tipo_usuario='ADMINISTRADOR_GERAL' and not is_deleted and coalesce(senha_hash,'') not like 'SIGOV_PBKDF2_V1$%') then raise exception 'Hash administrativo incompatível'; end if;
  if exists(select 1 from sigov.usuario u where u.tipo_usuario='ADMINISTRADOR_GERAL' and not u.is_deleted and not exists(select 1 from sigov.usuario_grupo ug where ug.usuario_id=u.id and not ug.is_deleted)) then raise exception 'Administrador sem grupo'; end if;
  if exists(select chave from sigov.permissao where not is_deleted group by chave having count(*)>1) then raise exception 'Permissões duplicadas'; end if;
end $$;
select 'security_runtime_ok';
