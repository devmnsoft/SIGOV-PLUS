# Runtime smoke tests

## Validação inicial — 2026-07-02
- `dotnet restore && dotnet build`: não executado porque o binário `dotnet` não está instalado no container (`dotnet: command not found`).
- `docker compose down && docker compose up -d --build && docker compose ps`: não executado porque o binário `docker` não está instalado no container (`docker: command not found`).
- Rotas HTTP locais: indisponíveis porque a aplicação não pôde ser iniciada neste ambiente.

## Validação final — 2026-07-02
- `dotnet restore`: bloqueado por ausência do SDK .NET no container.
- `dotnet build`: bloqueado por ausência do SDK .NET no container.
- `docker compose down`: bloqueado por ausência do Docker no container.
- `docker compose up -d --build`: bloqueado por ausência do Docker no container.
- `docker compose ps`: bloqueado por ausência do Docker no container.
- `curl -I --max-time 5 http://localhost:8080/Auth/Login`: falhou porque nenhum runtime foi iniciado.
- `curl -I --max-time 5 http://localhost:8080/Siafic`: falhou porque nenhum runtime foi iniciado.

Pendência: repetir a suíte completa em ambiente com .NET 6 SDK, Docker e PostgreSQL disponíveis.
