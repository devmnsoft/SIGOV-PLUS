# Entrega EXP08 — Ativos360

## Resultado

- Portal responsivo e dashboard premium integrando patrimônio, estoque e frota.
- URLs canônicas `/Ativos` e subrotas de almoxarifado, produtos, estoque, requisições, inventários, patrimônio, frotas, alertas e relatórios.
- Schema complementar idempotente, índices contextuais, checks financeiros/operacionais, RBAC e triggers de bloqueio.
- Manifest e cinco artefatos SQL completos sincronizados.

## Decisões

Não foram duplicadas entidades de produto, requisição ou inventário que já existiam. Rotas de edição/detalhe/transferência/baixa exigem seleção anterior, evitando inputs manuais de ID. Alertas e estados vazios refletem exclusivamente dados persistidos.

## Validação de ambiente

A execução final registra como `BLOCKED` qualquer ferramenta indisponível, conforme o gate do repositório. Nenhum resultado bloqueado é apresentado como aprovação.
