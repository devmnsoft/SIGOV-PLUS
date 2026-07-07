# Outbox worker e webhooks

O worker passa a consumir `sigov.outbox_evento` com status `PENDENTE`/`ERRO`, marca como `PROCESSANDO`, registra sucesso em `sigov.webhook_entrega` e conclui com status `ENTREGUE`. Falhas incrementam tentativas, reagendam a próxima tentativa e, no limite da política, marcam `FALHOU`.

Payloads registrados em entregas são resumidos/mascarados; erros são truncados e não incluem token claro ou dados pessoais completos.

Webhooks externos continuam dependentes de URL/secret configurados. Não há marcação de sucesso oficial sem tentativa real ou fallback explícito.
