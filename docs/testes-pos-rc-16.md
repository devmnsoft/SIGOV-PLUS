# Testes Pós-RC 16

## Criados

- `tests/Sigov.UnitTests/ArchitectureDependencyTests.cs`: verifica `ProjectReference` proibido em Domain, Application, Infrastructure, API, Web e Worker.

## Não executados localmente

- `dotnet test sigov.sln --configuration Release --no-build --logger trx --results-directory TestResults`: bloqueado por ausência de `dotnet` no container.
