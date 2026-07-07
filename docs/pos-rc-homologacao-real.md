# Pós-RC 02 — homologação real

Este ciclo conecta a API v1 de Protocolo e GED às tabelas reais `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.portal_validacao_documento` e `sigov.outbox_evento`.

## Classificação

- **Funcional real:** autenticação API v1 por `X-Api-Key`/`X-Tenant-Id`, escopos mínimos, logs em `sigov.api_requisicao_log`, criação/listagem/detalhe/tramitação de protocolo via API, criação/listagem/detalhe de documento via API, outbox worker sobre `sigov.outbox_evento`.
- **Parcial:** telas MVC de Protocolo/GED continuam com fallback honesto quando o formulário real não envia todos os campos necessários; as APIs já persistem nas tabelas Pós-RC.
- **Em implantação/fallback:** UI de API keys e webhooks mantém mensagens seguras quando schema/configuração estiver indisponível.
- **Dependente de provedor:** assinatura ICP/Gov.br, OCR real e integrações externas oficiais.
- **Não disponível:** simulação de assinatura oficial, simulação de OCR e simulação de entrega de webhook como sucesso sem envio real.

## LGPD e auditoria

As respostas da API evitam expor CPF, CNPJ, e-mail, telefone, CNS e prontuário completos. O token da API key é validado por hash SHA-256 e nunca é gravado no log de requisição.

## Pós-RC 03 — homologação Web real

- **Funcional real:** Protocolo e GED Web passam a acionar serviços Dapper para `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.protocolo_anexo`, `sigov.portal_validacao_documento` e `sigov.outbox_evento` quando o schema existe.
- **Parcial:** Dashboard, Minha Central, Busca e Relatórios mantêm fallback honesto e devem priorizar dados reais detectados no schema.
- **Em implantação/fallback:** PDF/DOCX da POC, OCR, ICP-Brasil e Gov.br não são simulados.
- **Dependente de provedor:** envio externo de webhook e validações oficiais dependem de infraestrutura configurada.
- **Não disponível:** exposição de path físico de storage e dados pessoais completos em listagens/exports.

## Pós-RC 04 — pacote final de homologação

- Seed idempotente: `database/postgres/seeds/pos_rc_homologacao_demo.sql`.
- Aplicação do seed: `pwsh -NoProfile -File scripts/apply-demo-seed.ps1`.
- Smoke test release candidate: `pwsh -NoProfile -File scripts/smoke-test-sigov.ps1`.
- Relatórios CSV reais adicionados para protocolos, documentos, tarefas, notificações, workflow, outbox, webhooks e auditoria operacional.
- Dados demonstráveis são fictícios, seguros e não devem ser aplicados em Production.
