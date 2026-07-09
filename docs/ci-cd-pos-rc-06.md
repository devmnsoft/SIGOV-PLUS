# CI/CD Pós-RC 06

## Jobs do CI

- `build-test`: restore, build Release e testes .NET.
- `docker-build`: build das imagens API, Web e Worker.
- `sql-validate`: PostgreSQL service, migrations em ordem, seed demo duas vezes, schema report via `psql`, validação de tabelas críticas, hash/escopos da API key demo e ausência de chave clara.
- `smoke-static`: checks estáticos dos bugs Pós-RC 06.
- `docker-compose-e2e`: sobe Docker Compose, aplica seed demo, roda smoke autenticado e coleta evidências.
- `release-package-check`: gera e valida pacote `artifacts/release/sigov-plus-1.0.0-rc-final`.

## Execução local equivalente

```bash
dotnet restore sigov.sln
dotnet build sigov.sln --configuration Release
dotnet test sigov.sln --configuration Release
docker compose down -v
docker compose build --no-cache
docker compose up -d
```

Depois aplique o seed e rode o smoke com PowerShell 7.
