# Runtime smoke tests SIGOV PLUS

## Validação inicial — 2026-07-02

- `dotnet restore && dotnet build`: não executado neste container porque o comando `dotnet` não está instalado (`/bin/bash: dotnet: command not found`).
- `docker compose down && docker compose up -d --build && docker compose ps`: não executado neste container porque o comando `docker` não está instalado.
- Smoke HTTP em `localhost:8080` e `localhost:5001`: falhou porque os containers não puderam subir neste ambiente.

## Validação final — 2026-07-02

Repetir em ambiente com .NET 6 SDK e Docker:

```powershell
dotnet restore
dotnet build
docker compose down
docker compose up -d --build
docker compose ps
```

Rotas a validar: `/Auth/Login`, `/Dashboard`, `/MinhaCentral`, `/Ia`, `/AssinaturasDigitais`, `/Integracoes`, `/Operacao/Logs`, `/Operacao/Metricas`, `/Operacao/Backup`, `/Seguranca/Politicas`, `/Poc`, `/Operacao/Health`, `/api/health/live`, `/api/v1/health`.
