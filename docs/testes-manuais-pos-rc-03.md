# Pós-RC 03 — Testes manuais de homologação real

Classificação: **Parcial / Funcional real quando schema PostgreSQL está disponível**.

## Fluxos obrigatórios

- **Protocolo Web real:** criar em `/Protocolo/Novo`, validar redirecionamento para `/Protocolo/Detalhes/{id}` e conferir registros em `sigov.protocolo`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao` e `sigov.outbox_evento`.
- **Tramitação Web real:** executar `/Protocolo/Tramitar/{id}` e validar `sigov.protocolo_movimento`, conclusão de tarefa anterior, criação de próxima tarefa, notificação e evento `protocolo.tramitado`.
- **GED Web real:** anexar em `/Ged/NovoDocumento`, validar SHA-256, `sigov.documento`, `sigov.documento_versao`, vínculo `sigov.protocolo_anexo` quando houver protocolo e validação pública para documento `PUBLICO`.
- **LGPD:** listas e CSVs devem mascarar CPF/CNPJ/e-mail/telefone e nunca exportar `storage_path`.
- **Permissões finas:** validar botões/actions com `protocolo.*`, `ged.*`, `workflow.*`, `tarefa.*`, `notificacao.*`, `api_key.gerenciar`, `webhook.gerenciar` e `relatorio.exportar`.
- **Fallback honesto:** se tabela/provedor não existir, a tela deve informar indisponibilidade sem gravar dado simulado.

## Evidências POC

Requisito crítico sem URL/ID real de protocolo, documento, workflow, tarefa, notificação, API key, webhook/outbox, CSV ou validação pública permanece **Não Atende**.
