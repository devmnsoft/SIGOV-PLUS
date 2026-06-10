# Financeiro comercial inicial

Esta etapa entrega contas a receber inicial sem implementar financeiro completo.

## Tabela

`sigov.financeiro_conta_receber` guarda origem, cliente, documento, parcela, valor original, valor aberto, vencimento e status por `tenant_id`.

## API

- `GET /api/financeiro/contas-receber`
- `POST /api/financeiro/contas-receber/{id}/receber`
- `POST /api/financeiro/contas-receber/{id}/cancelar`

## Integrações

- Venda a prazo gera conta a receber.
- Pedido faturado gera conta a receber.
- Baixa manual registra `CONTA_RECEBER_RECEBIDA`.
