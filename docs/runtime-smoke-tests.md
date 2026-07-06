# Runtime smoke tests

## 2026-07-06 — validação inicial/final neste runner

| Comando | Resultado | Observação |
|---|---|---|
| `dotnet restore` | Falhou | `dotnet: command not found` no ambiente. |
| `dotnet build` | Falhou | `dotnet: command not found` no ambiente. |
| `docker compose down` | Falhou | `docker: command not found` no ambiente. |
| `docker compose up -d --build` | Falhou | `docker: command not found` no ambiente. |
| `docker compose ps` | Falhou | `docker: command not found` no ambiente. |
| `curl http://localhost:8080/Auth/Login` e rotas principais | Falhou | Aplicação não pôde subir sem Docker/.NET; conexões recusadas. |

As falhas são limitação do ambiente de execução, não erro funcional confirmado do SIGOV PLUS. A sprint mantém fallback honesto e schema-safe até validação em ambiente com .NET 6, Docker e PostgreSQL.
