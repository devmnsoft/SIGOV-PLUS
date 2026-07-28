# Matriz de migração .NET 10

| Componente | Projeto | Framework após migração |
|---|---|---|
| Domain | `src/Sigov.Domain/Sigov.Domain.csproj` | net10.0 central |
| Application | `src/Sigov.Application/Sigov.Application.csproj` | net10.0 central |
| Infrastructure | `src/Sigov.Infrastructure/Sigov.Infrastructure.csproj` | net10.0 central |
| API | `src/Sigov.Api/Sigov.Api.csproj` | net10.0 central |
| Web | `src/Sigov.Web/Sigov.Web.csproj` | net10.0 central |
| Worker | `src/Sigov.Worker/Sigov.Worker.csproj` | net10.0 central |
| Testing | `tests/Sigov.Testing/Sigov.Testing.csproj` | net10.0 central |
| UnitTests | `tests/Sigov.UnitTests/Sigov.UnitTests.csproj` | net10.0 central |
| IntegrationTests | `tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj` | net10.0 central |
| ApiTests | `tests/Sigov.ApiTests/Sigov.ApiTests.csproj` | net10.0 central |

The SDK is pinned to 10.0.100 with `latestFeature`, without prereleases. Language and analyzer levels are pinned to 14.0 and 10.0 rather than floating values. API and Web use ASP.NET 10 images; Worker uses the .NET 10 runtime image.
