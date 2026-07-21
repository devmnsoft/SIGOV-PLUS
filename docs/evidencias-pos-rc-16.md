# Evidências Pós-RC 16

| Comando | Resultado |
|---|---|
| `dotnet --version` | Falhou: `dotnet: command not found` |
| `docker --version` | Falhou: `docker: command not found` |
| `docker compose version` | Falhou: `docker: command not found` |
| `psql --version` | Falhou: `psql: command not found` |
| `pwsh --version` | Falhou: `pwsh: command not found` |
| `rg "as IEnterpriseCrudService\|IEnterpriseCrudService\?" src -n` | Passou: nenhum resultado após correção |
| `git diff --check` | Passou após correções |
