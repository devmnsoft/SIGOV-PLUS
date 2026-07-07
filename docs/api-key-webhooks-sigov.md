# API Keys e Webhooks SIGOV

## API Keys

A API v1 exige `X-Api-Key` e `X-Tenant-Id`. A chave é comparada por SHA-256 em tempo constante contra `sigov.api_key.api_key_hash`. Os escopos são carregados de `sigov.api_key_escopo`.

Escopos mínimos: `protocolos.read`, `protocolos.write`, `documentos.read`, `documentos.write`, `tarefas.read`, `tarefas.write`, `notificacoes.read`, `webhooks.manage`, `mobile.sync`, `assinaturas.read`, `assinaturas.write`, `bi.read`.

## Webhooks

Eventos suportados: `protocolo.criado`, `protocolo.tramitado`, `documento.criado`, `documento.assinado`, `tarefa.criada`, `tarefa.concluida`, `contrato.criado`, `obra.medicao_registrada`, `manifestacao.recebida`, `chamado.aberto`, `sla.vencido`.
