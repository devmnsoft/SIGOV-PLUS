select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'tenant','usuario','implantacao','implantacao_etapa','implantacao_evidencia','migracao_lote','migracao_log','treinamento','treinamento_turma','treinamento_participante','treinamento_certificado','suporte_chamado','suporte_interacao','suporte_anexo','suporte_satisfacao','sla_regra','sla_evento','poc_roteiro','poc_requisito','poc_execucao','poc_evidencia','aceite_formal','contrato_operacional','auditoria_evento'
  )
order by table_name, ordinal_position;
