# Runtime smoke tests

## 2026-07-06 - validação inicial

- `dotnet restore && dotnet build`: não executado no ambiente porque o comando `dotnet` não está instalado (`/bin/bash: dotnet: command not found`).
- `docker compose down; docker compose up -d --build; docker compose ps`: não executado no ambiente porque o comando `docker` não está instalado.
- Rotas HTTP locais não testadas porque Docker/runtime ASP.NET não pôde ser iniciado neste container.

## Validação esperada em ambiente com .NET/Docker

- `dotnet restore`
- `dotnet build`
- `docker compose down`
- `docker compose up -d --build`
- `docker compose ps`
- `Invoke-WebRequest http://localhost:8080/Auth/Login -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/Dashboard -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/MinhaCentral -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/Editais -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/MatrizAderencia -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/Poc/Editais -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/Relatorios -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/Busca?q=edital -UseBasicParsing`
- `Invoke-WebRequest http://localhost:8080/Operacao/Health -UseBasicParsing`
- `Invoke-WebRequest http://localhost:5001/api/health/live -UseBasicParsing`

## 2026-07-06 - validação final

- `dotnet restore`: não executado no ambiente porque `dotnet` não está instalado.
- `dotnet build`: não executado no ambiente porque `dotnet` não está instalado.
- `docker compose down`: não executado no ambiente porque `docker` não está instalado.
- `docker compose up -d --build`: não executado no ambiente porque `docker` não está instalado.
- `docker compose ps`: não executado no ambiente porque `docker` não está instalado.
- Testes de rotas finais não executados porque a aplicação não pôde ser iniciada no container atual.
