# Matriz DI Pós-RC 16

| Área | Registro | Lifetime | Status | Observação |
|---|---|---|---|---|
| API | `EnterpriseDapperCrudService` concreto | Scoped | Corrigido | Instância concreta usada como origem dos contratos Enterprise |
| API | `IEnterpriseModuleService` | Scoped | Corrigido | Resolvido via `GetRequiredService<EnterpriseDapperCrudService>()` |
| API | `IEnterpriseCrudService` | Scoped | Corrigido | Evita cast entre contratos no controller |
| Web | `EnterpriseDapperCrudService` concreto | Scoped | Corrigido | Contratos compartilham a instância do mesmo scope |
| Web | `IEnterpriseModuleService` | Scoped | Corrigido | Mantém consumidores existentes |
| Web | `IEnterpriseCrudService` | Scoped | Corrigido | Usado diretamente pelo Kanban |
| Infrastructure | `EnterpriseDapperCrudService` concreto | Scoped | Corrigido | Registro central normalizado |
| Infrastructure | `IEnterpriseModuleService` / `IEnterpriseCrudService` | Scoped | Corrigido | Mapeamento explícito dos dois contratos |
| Web operacional | `TarefaService`, `AgendaOperacionalService`, `NotificacaoService` | Scoped | OK | Sem interface artificial |
