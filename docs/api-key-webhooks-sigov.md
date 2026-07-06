# API keys e Webhooks SIGOV PLUS

## Funcional real
- Rotas `/api/v1/*` exigem `X-Api-Key` e, para dados tenant, `X-Tenant-Id`.
- A API key é validada por hash SHA-256 armazenado em `sigov.api_key`; o token claro nunca é gravado em log ou tabela operacional.
- Escopos mínimos: `protocolos.read/write`, `documentos.read/write`, `tarefas.read/write`, `notificacoes.read`, `webhooks.manage`, `mobile.sync`, `assinaturas.read/write`, `bi.read`.
- Cada requisição é registrada em `sigov.api_requisicao_log` sem o token claro.

## Webhooks/outbox
- Eventos permitidos seguem a matriz Pós-RC (`protocolo.criado`, `protocolo.tramitado`, `documento.criado`, `tarefa.criada`, etc.).
- Payloads devem ser mínimos e mascarados antes de entrega.
- Entregas ficam em `sigov.webhook_entrega` com status, tentativas e erro mascarado.
