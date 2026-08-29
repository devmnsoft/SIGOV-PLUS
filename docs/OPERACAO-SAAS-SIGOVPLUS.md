# Operação SaaS Municipal

Tenant, entidade, unidade e exercício formam o contexto de isolamento. Parâmetros e ativação de módulos permanecem governados pelo banco; nenhuma coleção hardcoded pode substituir falha de schema.

Mudança de plano ou parâmetro crítico exige confirmação, justificativa e evento auditável. Um módulo somente pode ser ativado quando contrato/plano, permissão persistida e item de menu correspondente forem válidos. Limites de usuários, módulos, armazenamento lógico, integrações e exportações devem ser rejeitados de forma explícita quando excedidos.

## Operação e observabilidade

Os painéis `/Operacao/SaudeSistema`, `/Performance`, `/Migrations` e `/Logs` devem consultar eventos reais, com paginação e permissão administrativa. Exceptions são sanitizadas; tratamento exige justificativa. Outbox, DLQ, jobs e relatórios agendados nunca podem aparecer como saudáveis por fallback.
