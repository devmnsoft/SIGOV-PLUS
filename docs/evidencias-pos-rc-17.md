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

## Ajuste adicional nesta revisão

- `TarefaService` agora implementa explicitamente todos os membros de `ITarefaService`, delegando consultas ao repositório, validando transições e registrando histórico/outbox para criação e alteração de status.
- A migration Pós-RC 17 agora complementa `sigov.outbox_evento` quando a tabela já existe em formato legado, adicionando colunas operacionais padronizadas e retropreenchendo dados mínimos antes de aplicar `NOT NULL`.
