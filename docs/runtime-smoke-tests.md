# Runtime smoke tests

## 2026-07-01 — Sprint operacional de governo

### Baseline obrigatório

- `dotnet restore && dotnet build`: não executado por limitação do ambiente (`dotnet: command not found`).
- `docker compose down && docker compose up -d --build && docker compose ps`: não executado por limitação do ambiente (`docker: command not found`).
- Smoke HTTP local: pendente pelo mesmo motivo; sem runtime Docker disponível.

### Observação

As telas operacionais foram implementadas com fallback honesto: quando `IDatabaseSchemaInspector` não localizar tabelas em `sigov`, o usuário vê “Em implantação” e nenhuma action POST afirma sucesso de persistência.
