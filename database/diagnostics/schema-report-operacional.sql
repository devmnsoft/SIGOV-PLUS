select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'protocolo','processo','tramite','protocolo_movimento','protocolo_anexo','documento','ged_documento','ged_pasta','pasta','documento_versao','arquivo','ocr_fila','contribuinte','imovel','debito','guia','divida_ativa','contrato','contrato_aditivo','contrato_fiscal','contrato_documento','processo_juridico','parecer_juridico','prazo_juridico','audiencia_juridica','conta_pagar','conta_receber','caixa_movimento','categoria_financeira','auditoria_evento'
  )
order by table_name, ordinal_position;
