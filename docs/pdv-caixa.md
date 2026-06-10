# PDV web e caixa comercial

O PDV web inicial atende vendas não fiscais. Não há emissão NFC-e, SAT, TEF ou gateway real nesta etapa.

## PDV

- Busca rápida por produto/código de barras.
- Carrinho com quantidade, desconto, total e formas de pagamento.
- Finalização bloqueada sem itens ou sem pagamento total.
- Venda tipo `PDV` exige caixa aberto.

## Caixa

- `POST /api/comercio/caixas/abrir` abre caixa por tenant.
- `POST /api/comercio/caixas/{id}/suprimento` registra entrada operacional.
- `POST /api/comercio/caixas/{id}/sangria` registra retirada operacional.
- `POST /api/comercio/caixas/{id}/fechar` fecha com resumo por forma de pagamento.

Ações críticas devem exibir confirmação na UI e registrar auditoria (`CAIXA_ABERTO`, `CAIXA_FECHADO`, `CAIXA_SUPRIMENTO`, `CAIXA_SANGRIA`).
