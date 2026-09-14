# Integração Indústria, Comercial, Estoque, OS e Manutenção

## Comercial

`POST /api/comercio/pedidos/{id}/gerar-op` gera OP vinculada ao pedido quando `industria_producao` está ativo e o produto industrial está configurado.

## Estoque

`IIndustriaEstoqueService` registra movimentos `CONSUMO_PRODUCAO`, `ESTORNO_CONSUMO_PRODUCAO`, `ENTRADA_PRODUCAO`, `REFUGO_PRODUCAO` e `AJUSTE_PRODUCAO`. Quando `estoque_compras` não está ativo, a operação industrial continua com alerta e sem movimento físico.

## OS e manutenção

`POST /api/industria/paradas/{id}/gerar-os` abre uma ordem real em
`manutencao_ordem_servico`, exige o módulo `ordem_servico`, o contexto de entidade
e uma esfera de governo válida. A parada, a ordem e a auditoria
`PARADA_GEROU_OS` são persistidas na mesma transação.

A origem `(tenant_id, PARADA_INDUSTRIAL, parada_id)` é única. Assim, repetir a
requisição devolve a mesma ordem sem duplicar manutenção; uma parada de outro
tenant ou sem recurso produtivo não é aceita. O recurso industrial é registrado
como alvo `EQUIPAMENTO`, preservando o vínculo com a origem e sem simular uma OS
por meio de sequência desconectada.
