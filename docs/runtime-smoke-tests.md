# Runtime smoke tests SIGOV PLUS

## Como executar

```powershell
./scripts/smoke-test-sigov.ps1
```

## Resultado local desta sprint

Pendente de execução após Docker subir no ambiente local. O script grava status HTTP, duração e falhas por rota.

## Release Candidate 1.0.0-rc.2 — validação do agente em 2026-07-06

Comandos tentados:

```bash
dotnet clean && dotnet restore && dotnet build
```

Resultado: não executado por limitação do ambiente (`/bin/bash: dotnet: command not found`). Não foi possível avançar com validação técnica real de build/test neste container.

```bash
docker compose down; docker compose up -d --build; docker compose ps; docker compose logs --tail=200
```

Resultado: não executado por limitação do ambiente (`/bin/bash: docker: command not found`).

Correção aplicada nesta sprint: documentação de RC criada/atualizada e `scripts/smoke-test-sigov.ps1` revisado para rotas principais, status HTTP, resumo final e persistência de resultado em `docs/smoke-test-release-candidate.md`.
