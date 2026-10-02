select table_name, column_name from information_schema.columns where table_schema='sigov' and table_name in ('usuario_grupo','grupo_acesso','grupo_perfil','perfil_permissao','os_tecnico') order by table_name, ordinal_position;
select * from sigov.usuario_grupo where usuario_id in (101,102);
select * from sigov.grupo_perfil where perfil_acesso_id in (9001,9002,48) limit 10;
