# Protocolo, GED e Workflow reais

## Protocolo

`POST /api/v1/protocolos` gera número sequencial por tenant/exercício, persiste `sigov.protocolo`, cria `sigov.workflow_instancia`, tarefa inicial, notificação e evento `protocolo.criado` em `sigov.outbox_evento`.

`POST /api/v1/protocolos/{id}/tramitar` registra `sigov.protocolo_movimento`, conclui tarefas pendentes do protocolo, cria nova tarefa/notificação e publica `protocolo.tramitado`.

## GED

`POST /api/v1/documentos` calcula SHA-256, registra metadados em `sigov.documento`, versão em `sigov.documento_versao`, storage path local e evento `documento.criado`. Documento `PUBLICO` recebe código de validação em `sigov.portal_validacao_documento`.

## Fallback honesto

OCR e assinatura oficial permanecem dependentes de provedores reais e não são simulados como entrega oficial.
