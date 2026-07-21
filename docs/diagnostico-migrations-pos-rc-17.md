# Núcleo Operacional Transversal Pós-RC 17

Este incremento introduz contratos transversais para tarefas, agenda, prazos, notificações, Kanban e publicação de eventos operacionais na outbox, mantendo Dapper e PostgreSQL.

## Entregas implementadas

- Contratos de Application para `ITarefaRepository`, `ITarefaService`, `ITarefaHistoricoRepository`, `ITarefaNotificationService`, `IAgendaService`, `IPrazoOperacionalService`, `INotificacaoService`, `IKanbanService` e `IOperationalEventPublisher`.
- Implementação Dapper consolidada em repositório operacional.
- Máquina de transição de tarefas no serviço de aplicação.
- Migration PostgreSQL idempotente para tabelas operacionais e outbox padronizada.
- Registros de Dependency Injection para todos os contratos operacionais.

## Limitações reais

A execução local não possui SDK .NET, Docker, PostgreSQL ou PowerShell. Portanto, build, testes, Docker, migrations reais, smoke e go-live não puderam ser comprovados neste ambiente. Nenhum sucesso foi declarado sem execução.

## Revisão de compatibilidade outbox legado

A migration `20260721120000_pos_rc_17_runtime_nucleo_operacional.sql` foi ajustada para não depender de `CREATE TABLE IF NOT EXISTS` quando `sigov.outbox_evento` já existe em formato Pós-RC anterior. O script passa a usar `ALTER TABLE ... ADD COLUMN IF NOT EXISTS`, índice único parcial para `idempotency_key` e preenchimento seguro dos campos `event_id`, `event_type`, `aggregate_type`, `aggregate_id`, `attempts` e `next_attempt_at`.
