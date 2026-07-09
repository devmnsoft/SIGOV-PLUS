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

## Bloco Pós-RC 05 — demo homologável

1. Mostrar status do CI e explicar jobs de build/test, Docker e SQL.
2. Abrir Dashboard com dados do seed demo.
3. Criar e tramitar protocolo.
4. Anexar documento GED e validar documento público.
5. Executar Busca Global e exportar CSV com dados mascarados.
6. Mostrar API key criada, sem revelar segredo completo.
7. Mostrar outbox/webhook como operação monitorável, indicando dependências externas quando houver.
8. Encerrar com pacote de release e pendências honestas.

## Complemento Pós-RC 06

Para homologação técnica, aplicar `database/postgres/seeds/pos_rc_homologacao_demo.sql` e executar `scripts/smoke-test-sigov.ps1` com `SIGOV_SMOKE_USE_DEMO_KEY=true`. A chave demo local documentada é `sigov_demo_local_only_2026_please_rotate`, deve ser rotacionada antes de qualquer uso real e nunca é salva em claro no banco.

## Pós-RC 07 — Enterprise CRUD funcional

- Incluídas tabelas `sigov.enterprise_*` idempotentes para Comercial, OS, Estoque/Compras, Industrial/Manutenção, Indústria Produção, eventos e auditoria.
- Telas Enterprise existentes passam a usar template operacional com listagem real, formulário, detalhes, exportação CSV e avisos LGPD/fallback.
- Jornadas mínimas funcionais: proposta aprovada gera pedido; pedido gera OS; OS consome estoque; saldo negativo é bloqueado; plano preventivo gera OS.
