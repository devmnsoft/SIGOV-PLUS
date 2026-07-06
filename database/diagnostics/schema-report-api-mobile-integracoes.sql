select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'api_key',
    'api_key_escopo',
    'api_requisicao_log',
    'webhook_configuracao',
    'webhook_entrega',
    'evento_operacional',
    'outbox_evento',
    'campo_dispositivo',
    'campo_roteiro',
    'campo_coleta',
    'campo_evidencia',
    'campo_sincronizacao',
    'assinatura_documento',
    'assinatura_signatario',
    'assinatura_evento',
    'portal_validacao_documento',
    'integracao_sistema',
    'integracao_log',
    'bi_indicador',
    'bi_dashboard',
    'auditoria_evento'
  )
order by table_name, ordinal_position;
