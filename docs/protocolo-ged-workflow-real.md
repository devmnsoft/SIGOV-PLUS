# Protocolo, GED e Workflow reais

## Protocolo

`POST /api/v1/protocolos` gera número sequencial por tenant/exercício, persiste `sigov.protocolo`, cria `sigov.workflow_instancia`, tarefa inicial, notificação e evento `protocolo.criado` em `sigov.outbox_evento`.

`POST /api/v1/protocolos/{id}/tramitar` registra `sigov.protocolo_movimento`, conclui tarefas pendentes do protocolo, cria nova tarefa/notificação e publica `protocolo.tramitado`.

## GED

`POST /api/v1/documentos` calcula SHA-256, registra metadados em `sigov.documento`, versão em `sigov.documento_versao`, storage path local e evento `documento.criado`. Documento `PUBLICO` recebe código de validação em `sigov.portal_validacao_documento`.

## Fallback honesto

OCR e assinatura oficial permanecem dependentes de provedores reais e não são simulados como entrega oficial.

## Pós-RC 03 — homologação Web real

- **Funcional real:** Protocolo e GED Web passam a acionar serviços Dapper para `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.protocolo_anexo`, `sigov.portal_validacao_documento` e `sigov.outbox_evento` quando o schema existe.
- **Parcial:** Dashboard, Minha Central, Busca e Relatórios mantêm fallback honesto e devem priorizar dados reais detectados no schema.
- **Em implantação/fallback:** PDF/DOCX da POC, OCR, ICP-Brasil e Gov.br não são simulados.
- **Dependente de provedor:** envio externo de webhook e validações oficiais dependem de infraestrutura configurada.
- **Não disponível:** exposição de path físico de storage e dados pessoais completos em listagens/exports.
