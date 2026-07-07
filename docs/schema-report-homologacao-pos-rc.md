# Schema report — Homologação Pós-RC

Diagnóstico atualizado para a sprint Pós-RC. A execução automática dos scripts PowerShell ficou bloqueada no ambiente local porque `pwsh` não está instalado; a classificação abaixo foi derivada do inventário do repositório e da migration idempotente `20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql`.

| Tabela | Classificação | Observação |
|---|---|---|
| sigov.api_key | EXISTE E OK | Criada/completada para hash, prefixo, tenant, status e auditoria. |
| sigov.api_key_escopo | EXISTE E OK | Escopos granulares por API key. |
| sigov.api_requisicao_log | EXISTE E OK | Log sem token claro. |
| sigov.webhook_configuracao | EXISTE E OK | Configuração por tenant e eventos. |
| sigov.webhook_entrega | EXISTE E OK | Entregas com payload/erro mascarado. |
| sigov.outbox_evento | EXISTE E OK | Eventos pendentes, tentativas e correlação. |
| sigov.protocolo | EXISTE E OK | Base real para protocolo por tenant/exercício. |
| sigov.protocolo_movimento | EXISTE E OK | Movimentação/tramitação. |
| sigov.protocolo_anexo | EXISTE E OK | Vínculo protocolo-documento. |
| sigov.documento | EXISTE E OK | Metadados GED, hash e classificação LGPD. |
| sigov.documento_versao | EXISTE E OK | Versionamento GED. |
| sigov.ged_pasta | EXISTE E OK | Organização documental. |
| sigov.workflow* | EXISTE E OK | Workflow, etapas, transições, instâncias e histórico. |
| sigov.tarefa | EXISTE E OK | Tarefa operacional vinculável ao workflow/protocolo. |
| sigov.notificacao* | EXISTE E OK | Notificações e leitura por usuário. |
| sigov.portal_validacao_documento | EXISTE E OK | Validação pública por código/hash sem dado sensível. |
| Provedores oficiais de assinatura/OCR | NÃO USAR NESTA SPRINT | Permanecem dependentes de provedor/configuração real. |

## Pós-RC 02 — persistência real operacional

- Funcional real: API v1 com API key/tenant/escopos, Protocolo e GED persistindo nas tabelas Pós-RC, Outbox worker consumindo sigov.outbox_evento.
- Parcial: telas MVC administrativas continuam com fallback honesto quando ação/formulário não possui todos os dados reais.
- Dependente de provedor: OCR, ICP/Gov.br e entrega externa oficial de webhooks.
- LGPD: respostas e logs não devem expor dados pessoais completos nem token claro.

