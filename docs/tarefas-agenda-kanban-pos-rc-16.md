# Tarefas, Agenda e Kanban Pós-RC 16

- Tarefas e Agenda continuam expostas pelas mesmas rotas Web, com ações auditadas e mensagem honesta quando o schema real não estiver disponível.
- O Kanban deixou de depender de cast entre `IEnterpriseModuleService` e `IEnterpriseCrudService`.
- Em produção, ausência de tenant válido retorna mensagem de falha operacional e não usa tenant de demonstração.
- A política de transições permanece restrita às listas permitidas por tipo (`Tarefas`, `OS`, `Propostas`).
