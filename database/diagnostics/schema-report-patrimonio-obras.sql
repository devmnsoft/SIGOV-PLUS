select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'pessoa','endereco','rh_servidor','rh_lotacao','contrato','contrato_aditivo','contrato_fiscal','contrato_documento','almoxarifado_produto','almoxarifado_movimento','patrimonio_bem','patrimonio_grupo','patrimonio_localizacao','patrimonio_responsavel','patrimonio_movimento','patrimonio_inventario','patrimonio_inventario_item','patrimonio_baixa','patrimonio_depreciacao','obra','obra_contrato','obra_medicao','obra_diario','obra_foto','obra_ocorrencia','obra_fiscalizacao','obra_garantia','conta_pagar','empenho','liquidacao','pagamento','auditoria_evento'
  )
order by table_name, ordinal_position;
