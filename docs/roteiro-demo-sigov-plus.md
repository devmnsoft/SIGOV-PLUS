# Roteiro demo SIGOV PLUS — Pós-RC 04

## Preparação

1. Subir Docker.
2. Aplicar `scripts/apply-demo-seed.ps1`.
3. Executar `scripts/smoke-test-sigov.ps1`.
4. Confirmar que nenhuma rota principal retorna 404.

## Narrativa

- **Governança:** `/Dashboard` com protocolos, tarefas, documentos, notificações, webhooks e outbox.
- **Operação diária:** `/MinhaCentral` com pendências e atalhos por permissão.
- **Protocolo + Workflow:** criação, tramitação, tarefa e notificação.
- **GED:** documento com hash SHA-256, classificação LGPD e link de validação quando público.
- **Busca:** resultados em protocolo, movimento, documento, workflow, tarefa e notificação.
- **Relatórios:** CSVs com tenant, auditoria e mascaramento.
- **Integrações:** API keys com hash e webhooks sem secret claro.
- **POC:** requisitos críticos exigem evidência real; fallback não aprova.

## Evidências esperadas

- Protocolo `2026-000001` a `2026-000005`.
- Documentos `DEMO-DOC-PUB-001`, `DEMO-DOC-PUB-002` e `DEMO-DOC-RES-001`.
- Validação pública `PUB-DEMO-001` e `PUB-DEMO-002`.
- Outbox em estados `PENDENTE`, `ENTREGUE` e `FALHOU`.
