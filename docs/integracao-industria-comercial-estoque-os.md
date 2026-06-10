# Integração Indústria, Comercial, Estoque, OS e Manutenção

## Comercial

`POST /api/comercio/pedidos/{id}/gerar-op` gera OP vinculada ao pedido quando `industria_producao` está ativo e o produto industrial está configurado.

## Estoque

`IIndustriaEstoqueService` registra movimentos `CONSUMO_PRODUCAO`, `ESTORNO_CONSUMO_PRODUCAO`, `ENTRADA_PRODUCAO`, `REFUGO_PRODUCAO` e `AJUSTE_PRODUCAO`. Quando `estoque_compras` não está ativo, a operação industrial continua com alerta e sem movimento físico.

## OS e manutenção

Paradas podem sinalizar geração de OS por `POST /api/industria/paradas/{id}/gerar-os`, exigindo módulo `ordem_servico` ativo e gravando auditoria `PARADA_GEROU_OS`.
