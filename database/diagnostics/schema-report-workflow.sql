select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'workflow','workflow_etapa','workflow_transicao','workflow_instancia','workflow_historico',
    'tarefa','notificacao','notificacao_usuario','agenda_prazo','evento_operacional','outbox_evento',
    'integracao_sistema','integracao_log','protocolo','processo','documento','contrato','processo_juridico',
    'debito','conta_pagar','conta_receber','auditoria_evento'
  )
order by table_name, ordinal_position;
