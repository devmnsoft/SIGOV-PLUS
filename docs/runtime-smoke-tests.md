# Runtime smoke tests

Data da tentativa: 2026-07-01.

## Limitação do ambiente do agente

| Comando | Resultado | Correção aplicada | Pendência |
|---|---:|---|---|
| `dotnet restore` | Falhou: `dotnet: command not found` | Nenhuma correção de código dependente de build pôde ser validada localmente. | Executar em runner com .NET 6 SDK. |
| `dotnet build` | Falhou: `dotnet: command not found` | Código revisado para manter C# 10/.NET 6. | Executar em runner com .NET 6 SDK. |
| `docker --version` | Falhou: `docker: command not found` | Criado script de relatório de schema para ambiente Docker real. | Executar Docker Compose em host com Docker. |
| `docker compose version` | Falhou: `docker: command not found` | Smoke HTTP documentado abaixo para reexecução. | Executar em host com Docker Compose. |

## Rotas planejadas para validação HTTP

| URL | Status code | Resultado | Erro encontrado | Correção aplicada | Pendência |
|---|---:|---|---|---|---|
| http://localhost:8080/Auth/Login | Pendente | Não executado | Docker indisponível | N/A | Reexecutar após `docker compose up -d --build`. |
| http://localhost:8080/Dashboard | Pendente | Não executado | Docker indisponível | Dashboard mantém fallback honesto existente. | Reexecutar. |
| http://localhost:8080/MinhaCentral | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Manual | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Poc | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Saas/Tenants | Pendente | Não executado | Docker indisponível | Tenants preservado schema-safe. | Reexecutar. |
| http://localhost:8080/Saas/Modulos | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Saas/Parametros | Pendente | Não executado | Docker indisponível | Editor movido para `sigov.parametro_sistema` schema-safe com filtros, validação e restauração honesta. | Reexecutar. |
| http://localhost:8080/Seguranca/Usuarios | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Seguranca/Perfis | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Seguranca/Permissoes | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Relatorios | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Auditoria/Trilhas | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Lgpd/Dashboard | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Operacao/Health | Pendente | Não executado | Docker indisponível | Health fallback não declara API/Worker/Storage online sem prova. | Reexecutar. |
| http://localhost:8080/Protocolo | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Ged | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:8080/Tributario | Pendente | Não executado | Docker indisponível | N/A | Reexecutar. |
| http://localhost:5001/api/health/live | Pendente | Não executado | Docker indisponível | Health Web documenta que API deve ser validada por probe externo. | Reexecutar. |
