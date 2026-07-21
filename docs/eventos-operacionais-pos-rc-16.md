# Eventos operacionais Pós-RC 16

O fluxo Web operacional mantém registro em auditoria e encaminhamento via `OutboxSigovService`/`OperationalEventService` quando o schema está disponível. A rodada estabilizou DI e contratos antes de expandir o barramento para um contrato `IOperationalEventPublisher` compartilhado entre camadas.

Eventos observados no escopo atual:

- `KANBAN_STATUS_ALTERAR`
- ações de tarefas (`TAREFA_CRIAR`, `TAREFA_CONCLUIR`, `TAREFA_REABRIR`, `TAREFA_DELEGAR`)
- ações contratuais emitidas por `OutboxSigovService`
