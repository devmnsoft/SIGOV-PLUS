select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'usuario',
    'tenant',
    'perfil',
    'permissao',
    'perfil_permissao',
    'tenant_modulo_contratado',
    'parametro_sistema',
    'plano_saas',
    'auditoria_evento',
    'protocolo',
    'processo',
    'tramite',
    'protocolo_movimento',
    'documento',
    'ged_documento',
    'pasta',
    'documento_versao',
    'arquivo',
    'contribuinte',
    'imovel',
    'debito',
    'guia',
    'divida_ativa'
  )
order by table_name, ordinal_position;
