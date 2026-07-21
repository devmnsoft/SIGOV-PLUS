# Matriz de interfaces Pós-RC 16

| Classe | Namespace | Responsabilidade | Camada | Interface | Implementação | DI | Lifetime | Consumidores | Testabilidade | Decisão | Ação |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `EnterpriseDapperCrudService` | `Sigov.Infrastructure` | CRUD Enterprise Dapper e módulos operacionais | Infrastructure | `IEnterpriseModuleService`, `IEnterpriseCrudService` | Sim | Sim | Scoped | API, Web, Kanban, controllers Enterprise | Mockável por contrato | INTERFACE_OK | Normalizado para uma implementação scoped compartilhada |
| `TarefaService` | `Sigov.Web.Services.Operational` | Hub transversal de tarefas com schema real/fallback honesto | Web | Sem interface | n/a | Sim | Scoped | `TarefasController` | Testável por DI concreta no Web | OK_SEM_INTERFACE | Mantido sem interface artificial |
| `AgendaOperacionalService` | `Sigov.Web.Services.Operational` | Agenda e prazos operacionais | Web | Sem interface | n/a | Sim | Scoped | `AgendaController` | Testável por dependências mockáveis | OK_SEM_INTERFACE | Mantido sem interface artificial |
| `NotificacaoService` | `Sigov.Web.Services.Operational` | Notificações internas em hub operacional | Web | Sem interface | n/a | Sim | Scoped | `NotificacoesController` | Testável por dependências mockáveis | OK_SEM_INTERFACE | Mantido sem interface artificial |
| `OperationalEventService` | `Sigov.Web.Services.Operational` | Registro operacional schema-safe | Web | Sem interface | n/a | Sim | Scoped | Workflow/Outbox Web | Testável por dependências mockáveis | OK_SEM_INTERFACE | Mantido, sem contrato artificial nesta rodada |
| `OutboxSigovService` | `Sigov.Web.Services.Operational` | Enfileiramento operacional local para outbox/evento | Web | Sem interface | n/a | Sim | Scoped | Serviços contratuais | Testável por dependências mockáveis | OK_SEM_INTERFACE | Mantido |
| `AuditTrailService` | `Sigov.Web.Services` | Auditoria Web | Web | `IAuditTrailService` | Sim | Sim | Scoped | Controllers Web | Mockável | INTERFACE_OK | Mantido |
| `LocalFileStorageService` | `Sigov.Infrastructure.Storage` | Storage local | Infrastructure | `IFileStorageService` | Sim | Sim | Scoped | GED/anexos | Mockável | INTERFACE_OK | Mantido |
| `OutboxService` | `Sigov.Infrastructure.Integracoes` | Outbox de integrações | Infrastructure | `IOutboxService` | Sim | Sim | Scoped | API/Worker integrações | Mockável | INTERFACE_OK | Mantido |
