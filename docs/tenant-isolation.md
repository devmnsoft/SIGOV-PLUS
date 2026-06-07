# Tenant isolation

## Regras

- Queries operacionais devem filtrar `tenant_id`.
- Acesso cruzado deve retornar 404 ou 403.
- Exportações devem respeitar tenant.
- Scripts de resíduo ajudam a impedir schemas antigos; todos os objetos SQL versionados devem permanecer em `sigov`.

## Validação

Execute `dotnet test sigov.sln` e priorize os testes de integração de módulos existentes. Novos testes condicionais devem ser adicionados conforme endpoints CRUD por módulo forem estabilizados.
