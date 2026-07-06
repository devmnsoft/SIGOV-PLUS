select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'edital',
    'edital_documento',
    'edital_requisito',
    'edital_requisito_modulo',
    'edital_matriz_aderencia',
    'edital_evidencia',
    'edital_poc_roteiro',
    'edital_poc_execucao',
    'edital_poc_item',
    'edital_poc_evidencia',
    'edital_relatorio_tecnico',
    'edital_anexo',
    'modulo_saas',
    'tenant_modulo_contratado',
    'auditoria_evento'
  )
order by table_name, ordinal_position;
