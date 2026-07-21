# Matriz de referências Pós-RC 22

Validação estática executada neste ciclo focou nos erros confirmados de build:

- `AuthController` referencia `IAuditTrailService` do namespace `Sigov.Web.Services`, já registrado em DI por `Program.cs`.
- `TarefasController`, `AgendaController`, `KanbanController` e `MobileCampoController` permanecem em `OperationalTransversalController.cs`.
- `MobileController`, `CampoController` e `OfflineController` permanecem em `MobileCampoController.cs` sem a classe duplicada.
- Testes de integração usam `TestRepoPath` para resolver a raiz do repositório sem caminho absoluto.

Ferramentas de build, teste e DI completa não estavam disponíveis no container atual.
