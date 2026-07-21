# Diagnóstico inicial Pós-RC 16

## Ambiente registrado

- Branch inicial observada: `work`.
- SHA inicial: `2b1214a469f90d20654a0155c2204d921c461dd6`.
- Branch criada para correção: `codex/pos-rc-16-estabilizacao-arquitetural`.
- `dotnet --version`: indisponível no runner local (`dotnet: command not found`).
- `docker --version`: indisponível no runner local (`docker: command not found`).
- `docker compose version`: indisponível no runner local (`docker: command not found`).
- `psql --version`: indisponível no runner local (`psql: command not found`).
- `pwsh --version`: indisponível no runner local (`pwsh: command not found`).

## Projetos da solution

- `src/Sigov.Api/Sigov.Api.csproj`
- `src/Sigov.Application/Sigov.Application.csproj`
- `src/Sigov.Domain/Sigov.Domain.csproj`
- `src/Sigov.Infrastructure/Sigov.Infrastructure.csproj`
- `src/Sigov.Web/Sigov.Web.csproj`
- `src/Sigov.Worker/Sigov.Worker.csproj`
- `tests/Sigov.ApiTests/Sigov.ApiTests.csproj`
- `tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj`
- `tests/Sigov.UnitTests/Sigov.UnitTests.csproj`

## Migrations PostgreSQL

As migrations sob `database/postgres/migrations` foram inventariadas por ordem alfabética. A sequência inicia em `001_create_sigov_schema.sql` e segue até `20260713120000_pos_rc_11_enterprise_anexos_release.sql`.

## Erros reproduzíveis por inspeção estática

| Prioridade | Arquivo | Linha inicial | Erro | Causa provável | Ação |
|---|---:|---:|---|---|---|
| Alta | `src/Sigov.Web/Controllers/OperationalTransversalController.cs` | 12 | Controller comprimido, múltiplos campos/injeções na mesma linha | Código acumulado em Pós-RC anterior sem formatação | Reformatar preservando rotas |
| Alta | `src/Sigov.Web/Controllers/OperationalTransversalController.cs` | 143 | Casting `IEnterpriseModuleService as IEnterpriseCrudService` | Registro DI não expressava explicitamente os dois contratos | Injetar `IEnterpriseCrudService` diretamente |
| Alta | `src/Sigov.Api/Controllers/EnterpriseModulesController.cs` | 16 | Casting de contrato para contrato opcional | Contratos Enterprise resolvidos de forma implícita | Injetar `IEnterpriseCrudService` e registrar implementação compartilhada |
| Média | `src/Sigov.Api/Program.cs` | 32 | Registro DI parcial do Enterprise CRUD | Apenas `IEnterpriseModuleService` registrado na borda | Registrar implementação concreta scoped e mapear ambos os contratos |
| Média | `src/Sigov.Web/Program.cs` | 42 | Registro DI parcial do Enterprise CRUD | Mesmo serviço consumido por dois contratos | Registrar ambos os contratos com a mesma instância do scope |
| Média | `tests/Sigov.UnitTests` | n/a | Ausência de teste arquitetural de referências | Dependência entre camadas sem barreira automatizada | Adicionar teste estático de ProjectReference |

## Resultado inicial dos comandos solicitados

Os comandos `dotnet clean`, `dotnet restore`, `dotnet build`, `dotnet test`, `dotnet format`, Docker, `psql` e PowerShell não puderam ser executados neste container porque os binários não estão instalados. Esse fato foi registrado como limitação de ambiente; não foi tratado como sucesso.

## CI conhecido

O enunciado da tarefa informa que o CI mais recente estava vermelho: restore passou; build, migrations, Docker, compose, smoke-static e release falharam; testes e go-live foram ignorados em cascata. Não havia CLI do GitHub (`gh`) disponível para baixar logs adicionais neste container.
