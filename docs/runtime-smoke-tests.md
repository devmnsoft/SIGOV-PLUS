# Runtime smoke tests

## 2026-07-02 — Sprint operacional real

Ambiente desta sessão não possui `dotnet` nem `docker`; os comandos obrigatórios foram tentados e falharam por limitação de ambiente antes de alterações.

- `dotnet restore && dotnet build`: falhou com `/bin/bash: dotnet: command not found`.
- `docker compose down && docker compose up -d --build && docker compose ps`: falhou com `/bin/bash: docker: command not found`.

Rotas a validar em ambiente com Docker/.NET: `/Auth/Login`, `/Dashboard`, `/MinhaCentral`, `/Protocolo`, `/Ged`, `/Tributario`, `/Contratos`, `/Juridico`, `/Financeiro`, `/Relatorios`, `/Busca?q=teste`, `/Poc`, `/Operacao/Health`, `http://localhost:5001/api/health/live`.

## Validação final obrigatória — resultado nesta sessão

- `dotnet restore`: não executado por ausência do SDK (`dotnet: command not found`).
- `dotnet build`: não executado por ausência do SDK (`dotnet: command not found`).
- `docker compose down/up/ps`: não executado por ausência do Docker (`docker: command not found`).
- Smoke HTTP com `curl` para as rotas obrigatórias: não conectou porque os containers não puderam subir neste ambiente.
