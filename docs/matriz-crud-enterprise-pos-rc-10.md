# Matriz CRUD Enterprise Pós-RC 10

| Área | Rota Web | API | Criar | Editar | Inativar | Restaurar | Ações |
|---|---|---|---|---|---|---|---|
| Clientes | `/Enterprise/Comercial` e rotas Comercial | `/api/comercial/clientes` | Sim | Sim | Sim | Sim | CSV seguro |
| Propostas | Comercial/Propostas | `/api/comercial/propostas` | Sim | Sim | Sim | Sim | Aprovar, reprovar, gerar pedido |
| Pedidos | Comercial/Pedidos | `/api/comercial/pedidos` | Sim | Sim | Sim | Sim | Confirmar, cancelar, gerar OS |
| OS | `/OrdemServico/Ordens` | `/api/os/ordens` | Sim | Sim | Sim | Sim | Agendar, iniciar, pausar, checklist, apontamento, consumir peça, concluir, cancelar |
| Produtos | `/Estoque/Produtos` | `/api/estoque/produtos` | Sim | Sim | Sim | Sim | Entrada, saída, ajuste, saldo |
| Fornecedores | `/ComprasComercial/Fornecedores` | `/api/compras/fornecedores` | Sim | Sim | Sim | Sim | CSV seguro |
| Industrial | `/Industrial/*` | `/api/industrial/*` | Sim | Sim | Sim | Sim | OS preventiva, leitura, parada |
