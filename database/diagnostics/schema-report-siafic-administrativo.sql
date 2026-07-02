select table_schema,
       table_name,
       column_name,
       data_type,
       is_nullable,
       column_default
from information_schema.columns
where table_schema = 'sigov'
  and table_name in (
    'exercicio','entidade','unidade_orcamentaria','programa_governo','acao_governo','fonte_recurso','natureza_receita','natureza_despesa','plano_contas','dotacao_orcamentaria','bloqueio_orcamentario','empenho','liquidacao','pagamento','receita_arrecadada','conta_bancaria','conciliacao_bancaria','compra_solicitacao','compra_item','licitacao','licitacao_item','fornecedor','contrato','contrato_aditivo','almoxarifado_produto','almoxarifado_movimento','patrimonio_bem','patrimonio_movimento','frota_veiculo','frota_abastecimento','frota_manutencao','obra','obra_medicao','obra_diario','obra_foto','auditoria_evento'
  )
order by table_name, ordinal_position;
