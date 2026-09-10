# SIGOV PLUS

SIGOV PLUS e uma plataforma SaaS de gestao publica e operacao integrada para entidades municipais, estaduais e federais, com suporte tambem a organizacoes empresariais quando o modulo contratado exigir.

Esta pagina descreve o estado operacional atual. Documentos historicos de Pos-RC e FUNC permanecem no repositorio como memoria tecnica, mas nao promovem modulo a funcional, homologado ou producao sem evidencia runtime atual.

## Stack normativa

- .NET 10 conforme `global.json`.
- C# 14.
- ASP.NET Core MVC/Razor, API REST e Worker.
- PostgreSQL 16+ como persistencia oficial.
- Dapper; Entity Framework nao deve ser introduzido.
- Clean Architecture com `Domain`, `Application`, `Infrastructure`, `Api`, `Web` e `Worker`.

## Execucao local principal

A execucao local principal nao depende de Docker. Configure uma instancia PostgreSQL 16+ local, exporte `ConnectionStrings__DefaultConnection` e use os scripts locais:

```powershell
pwsh -NoProfile -File scripts/start-local.ps1
```

Linux/macOS:

```bash
./scripts/start-local.sh
```

URLs padrao:

- Web: `http://localhost:5000`
- API: `http://localhost:5001`
- Swagger: `http://localhost:5001/swagger`
- Health live: `http://localhost:5001/api/health/live`
- Health ready: `http://localhost:5001/api/health/ready`
- DB health: `http://localhost:5001/api/health/db`

## Docker opcional

Docker Compose e alternativa para validacoes de container e ambientes descartaveis:

```powershell
copy .env.example .env
docker compose up -d --build
```

Use Docker apenas como apoio operacional. O fluxo principal de desenvolvimento local continua sendo PostgreSQL instalado/configurado diretamente e processos .NET iniciados pelos scripts locais.

## Banco de dados

O PostgreSQL usa o schema fisico `sigov`. Multi-tenancy usa banco/schema compartilhado, com `tenant_id`, `entidade_id`, `exercicio_id`, usuario e escopo preservados nas operacoes em que se aplicam.

Scripts canonicos:

- `database/postgres/migrations/manifest.json`
- `database/postgres/script_completo.sql`
- `database/postgres/script_completo_dev.sql`
- `database/script_completo.sql`
- `script_completop.sql`
- `script_completo.sql`

`script_completop.sql` e autonomo e deve ser executado com PostgreSQL 16+:

```bash
psql -v ON_ERROR_STOP=1 -h localhost -p 5432 -U sigov -d sigov -f script_completop.sql
```

Toda alteracao de schema exige migration PostgreSQL idempotente, forward-only, sincronizada com manifesto e scripts consolidados. Migrations publicadas nao devem ser editadas.

## Build e testes

Build runtime:

```bash
dotnet clean sigov.runtime.slnf
dotnet restore sigov.runtime.slnf --locked-mode
dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror
```

Solucao completa e suites:

```bash
dotnet restore sigov.sln --locked-mode
dotnet build sigov.sln --configuration Release --no-restore --nologo -warnaserror
dotnet test tests/Sigov.UnitTests/Sigov.UnitTests.csproj --configuration Release --no-build
dotnet test tests/Sigov.ApiTests/Sigov.ApiTests.csproj --configuration Release --no-build
dotnet test tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj --configuration Release --no-build
```

## Estado funcional

O estado oficial dos modulos esta em `docs/execucao/STATUS_REAL_MODULOS.md`. Ate que um fluxo tenha migration, persistencia Dapper, regras, API/controller, UI real, validacao, autorizacao, isolamento de tenant, auditoria, transacao/concorrencia, estados de erro/vazio, testes criticos, menu por contratacao e evidencia runtime conjunta, ele deve ser tratado como `PARCIAL`, `ESTRUTURA` ou `AGUARDA_GATE`, conforme a matriz vigente.

Nao declare modulo como concluido apenas por existir controller, view, migration, seed ou documento.

## Seguranca e configuracao

Segredos devem vir do ambiente ou de secret manager. `.env` e `.env.local` sao locais e ignorados. Seeds de desenvolvimento/homologacao devem ser ficticias, idempotentes e nunca conter senha, token, chave real ou dado pessoal real.

Autorizacao, perfis, permissoes, parametros, catalogo SaaS e entitlements usam o banco como fonte de autoridade. Ausencia de schema ou configuracao deve falhar explicitamente; nao simule sucesso.

## Escopo atual

A trilha ativa RC51.00 deve fechar o P0 antes de liberar SaaS Admin e Ordem de Producao do Industria 360. GED permanece fora desta sprint.
