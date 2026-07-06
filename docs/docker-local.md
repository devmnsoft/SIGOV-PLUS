# SIGOV — Ambiente Docker local

Este guia descreve o ambiente Docker completo para desenvolvimento local e homologação técnica do SIGOV no estado atual do repositório (`sigov.sln`, `src/Sigov.Web`, `src/Sigov.Api`, `src/Sigov.Worker`).

## Pré-requisitos

- Docker Desktop no Windows com Docker Compose v2.
- Git.
- .NET SDK somente para execução fora do container.
- Não é necessário instalar PostgreSQL na máquina local.

## Primeiro uso

No PowerShell, a partir da raiz do repositório:

```powershell
copy .env.example .env
docker compose up -d --build
```

Também é possível usar o script:

```powershell
scripts/docker-start.ps1
```

O script cria `.env` a partir de `.env.example` se o arquivo ainda não existir.

## Serviços criados

| Serviço | Container | Função |
| --- | --- | --- |
| `postgres` | `sigov-postgres` | PostgreSQL 16 com banco criado automaticamente por `POSTGRES_DB`. |
| `db-migrations` | `sigov-db-migrations` | Aplica `database/apply_all_required_migrations.sql` e scripts SQL versionados. |
| `api` | `sigov-api` | API ASP.NET Core com Dapper/Npgsql e healthchecks. |
| `worker` | `sigov-worker` | Worker de outbox, desabilitado por padrão no Docker local. |
| `web` | `sigov-web` | Aplicação Web MVC/Razor publicada em `http://localhost:8080`. |

## URLs

- Web: <http://localhost:8080>
- API (desenvolvimento): <http://localhost:5001>
- API health live: <http://localhost:5001/api/health/live>
- API health ready: <http://localhost:5001/api/health/ready>

## Banco de dados

Configuração padrão do `.env.example`:

- Host local: `localhost`
- Host dentro do Docker: `postgres`
- Porta: `5432`
- Database: `postgres`
- Usuário: `postgres`
- Senha: conforme `.env`
- Schema aplicado: `sigov`

A connection string dos containers usa `Host=postgres`, enquanto a execução fora do Docker pode continuar usando `localhost`/`127.0.0.1` nos appsettings ou variáveis locais.

## Migrations automáticas

Ao executar `docker compose up -d --build`, o serviço `db-migrations`:

1. Aguarda o PostgreSQL ficar saudável com `pg_isready`.
2. Aplica `database/apply_all_required_migrations.sql`.
3. Aplica `database/postgres/migrations/*.sql` em ordem alfabética.
4. Aplica `database/migrations/*.sql` em ordem alfabética.
5. Registra cada arquivo em `sigov.docker_schema_migrations` com checksum.
6. Falha com exit code diferente de zero se qualquer SQL falhar ou se uma migration já aplicada tiver checksum diferente.

Para reaplicar manualmente:

```powershell
scripts/docker-apply-migrations.ps1
```

## Storage persistente

O volume Docker `sigov_storage` é montado em `/app/storage` para API/Web. O caminho pode ser alterado com:

```dotenv
APP_STORAGE_PATH=/app/storage
```

## Workers, OCR e preview

No Docker local, rotinas pesadas e workers opcionais ficam desabilitados por padrão:

```dotenv
OCR_WORKER_ENABLED=false
PREVIEW_WORKER_ENABLED=false
LOAN_OVERDUE_WORKER_ENABLED=false
OUTBOX_WORKER_ENABLED=false
```

Habilite apenas quando as dependências externas necessárias estiverem configuradas.

## Scripts Windows

| Script | Ação |
| --- | --- |
| `scripts/docker-start.ps1` | Cria `.env` se necessário e executa `docker compose up -d --build`. |
| `scripts/docker-stop.ps1` | Executa `docker compose down`. |
| `scripts/docker-reset.ps1` | Exige digitar `RESETAR BANCO`, remove volumes e recria o ambiente. |
| `scripts/docker-logs.ps1` | Exibe logs de `web`, `api`, `worker`, `postgres` e `db-migrations`. |
| `scripts/docker-apply-migrations.ps1` | Executa novamente o serviço `db-migrations`. |
| `scripts/docker-psql.ps1` | Abre `psql` dentro do container PostgreSQL. |
| `scripts/docker-validate.ps1` | Executa config, build, up, healthchecks, logs de migration e validação do schema. |

Há wrappers `.cmd` equivalentes para cada script PowerShell.

## Comandos úteis

```powershell
docker compose config
docker compose build
docker compose up -d --build
docker compose ps
docker logs sigov-db-migrations
docker logs sigov-web
scripts/docker-psql.ps1
```

Validar schema no banco:

```powershell
docker exec sigov-postgres psql -U postgres -d postgres -c "\dn"
docker exec sigov-postgres psql -U postgres -d postgres -c "select current_database();"
```

## Reset total

O reset remove volumes Docker, portanto apaga o banco e storage do ambiente local. Ele exige confirmação textual:

```powershell
scripts/docker-reset.ps1
# Digite: RESETAR BANCO
```

## Produção/homologação técnica

Use `docker-compose.prod.yml` como base para produção. Ele não expõe o PostgreSQL publicamente e exige secrets por variáveis de ambiente, especialmente `POSTGRES_PASSWORD` e `ConnectionStrings__DefaultConnection`.

## Complemento Release Candidate 1.0.0-rc.2

Sequência obrigatória para validação Docker da RC:

```powershell
docker compose down
docker compose up -d --build
docker compose ps
docker compose logs --tail=200
scripts/smoke-test-sigov.ps1
```

Validar PostgreSQL, migrations, API, Worker, Web, storage local, variáveis de ambiente e ausência de restart loop. Nesta execução do agente, Docker não estava instalado no container e a evidência foi registrada em `docs/runtime-smoke-tests.md`.
