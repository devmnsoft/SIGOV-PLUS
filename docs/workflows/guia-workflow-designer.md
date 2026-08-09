# Workflow Designer RC49

## Operação

1. Acesse **Workflows** com `WORKFLOW_CONSULTA`.
2. Crie uma definição com `WORKFLOW_GERENCIAR`; ela nasce em `RASCUNHO` e isolada pelo tenant autenticado.
3. No designer, adicione no mínimo duas etapas, exatamente uma inicial e ao menos uma final.
4. Salve o desenho. Todas as mutações usam antiforgery, transação, SQL parametrizado e auditoria.
5. Publique. A versão recebe um snapshot e a definição publicada fica somente leitura.

As rotas antigas de `/Workflow` continuam disponíveis. O módulo RC49 usa `/Workflows` para não alterar integrações legadas. A primeira entrega operacional cobre definição, designer, validação, publicação e versionamento. Execução integrada, formulários, portal, SLA e aprovações permanecem como próximas verticais, sobre as tabelas e contratos próprios, sem dados simulados.

## Segurança e homologação

O backend exige as policies canônicas, resolve `tenant_id` no contexto e inclui o tenant em toda consulta e mutação. Execute `scripts/check-rc49-platform-flows.ps1`; com PostgreSQL/runtime disponíveis ele também valida schema e rotas vivas.
