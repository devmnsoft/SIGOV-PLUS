# Matriz de testes Pós-RC 27A

- DI: `tests/Sigov.UnitTests/Sigov.UnitTests.csproj` com filtro `FullyQualifiedName~DependencyInjectionResolutionTests`.
- Web smoke: `tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj` com filtro `FullyQualifiedName~WebRuntimeSmokeTests`.
- Testes completos: `dotnet test sigov.sln --configuration Release --no-build --logger trx --results-directory TestResults`.
- Validação TRX: `scripts/validate-trx-results.py`, falhando com zero testes, falhas ou skips injustificados.
