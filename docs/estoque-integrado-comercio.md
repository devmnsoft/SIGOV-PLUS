# Estoque integrado ao comércio

O serviço `IComercioEstoqueService` centraliza reserva, baixa, estorno, disponibilidade e saldo de produtos.

## Movimentos

- `VENDA`
- `CANCELAMENTO_VENDA`
- `RESERVA_PEDIDO`
- `BAIXA_PEDIDO`
- `ESTORNO_PEDIDO`

## Regras

- Produto inativo não pode ser vendido.
- Venda finalizada baixa estoque.
- Venda cancelada estorna estoque quando já baixada.
- Pedido confirmado reserva estoque.
- Estoque negativo exige permissão `comercio.estoque.vender_negativo`.
- Sem módulo `estoque_compras`, a venda pode seguir com alerta operacional e sem baixa integrada completa.
