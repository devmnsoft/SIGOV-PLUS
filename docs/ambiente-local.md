# SIGOV - Ambiente Local

## Subir ambiente

```bash
docker compose up -d
```

## Endereços

Web:
http://localhost:8080

API:
http://localhost:5001

Health API:
http://localhost:5001/api/health/live

PostgreSQL:
Host: localhost
Porta: 5432
Database: postgres
Usuário: postgres
Senha: 123456

## Credenciais de desenvolvimento

Login: `admin`
Senha: `Admin@123`

> Credencial exclusiva para ambiente local de desenvolvimento. A senha é persistida com hash PBKDF2 compatível com o padrão `SIGOV_PBKDF2_V1` do projeto.

## Comandos úteis

```bash
docker compose ps
docker compose logs --tail=200 api
docker compose logs --tail=200 web
docker compose logs --tail=200 worker
docker compose logs --tail=200 db-migrations
docker compose logs --tail=200 postgres
```

## Build local

```bash
dotnet restore sigov.sln
dotnet build sigov.sln
dotnet test sigov.sln
```

## Scripts de apoio

```powershell
./scripts/check-local.ps1
./scripts/demo-local.ps1
```

## Observações

- O serviço db-migrations deve concluir com exit 0.
- API e Web dependem das migrations.
- Em caso de erro, verificar primeiro db-migrations e health da API.
- O dashboard inicial usa dados SaaS globais e exibe fallback visual caso a API não responda.
