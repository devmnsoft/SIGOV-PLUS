# Outbox worker e webhooks

O worker passa a consumir `sigov.outbox_evento` com status `PENDENTE`/`ERRO`, marca como `PROCESSANDO`, registra sucesso em `sigov.webhook_entrega` e conclui com status `ENTREGUE`. Falhas incrementam tentativas, reagendam a próxima tentativa e, no limite da política, marcam `FALHOU`.

Payloads registrados em entregas são resumidos/mascarados; erros são truncados e não incluem token claro ou dados pessoais completos.

Webhooks externos continuam dependentes de URL/secret configurados. Não há marcação de sucesso oficial sem tentativa real ou fallback explícito.

## Pós-RC 03 — homologação Web real

- **Funcional real:** Protocolo e GED Web passam a acionar serviços Dapper para `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.protocolo_anexo`, `sigov.portal_validacao_documento` e `sigov.outbox_evento` quando o schema existe.
- **Parcial:** Dashboard, Minha Central, Busca e Relatórios mantêm fallback honesto e devem priorizar dados reais detectados no schema.
- **Em implantação/fallback:** PDF/DOCX da POC, OCR, ICP-Brasil e Gov.br não são simulados.
- **Dependente de provedor:** envio externo de webhook e validações oficiais dependem de infraestrutura configurada.
- **Não disponível:** exposição de path físico de storage e dados pessoais completos em listagens/exports.
