# Operação SIGOV+ RC50.52

## Requisitos e contrato de banco
- .NET SDK conforme `global.json` e PostgreSQL 16 ou superior.
- Banco: `postgres`; schema e Search Path: `sigov`. Não criar database `sigov`.
- Desenvolvimento local: `Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=<senha-local>;Search Path=sigov;Application Name=sigov`.

## Instalação, build e execução
```bash
PGPASSWORD='<senha-local>' psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql
dotnet restore sigov.runtime.slnf --locked-mode
dotnet build sigov.runtime.slnf --configuration Release --no-restore --nologo -warnaserror
dotnet run --project src/Sigov.Api/Sigov.Api.csproj
dotnet run --project src/Sigov.Web/Sigov.Web.csproj
```
Swagger: `https://localhost:7001/swagger`. Health: `https://localhost:7001/health`.

## Diagnóstico
Use `./scripts/smoke-local.sh` (ou `.\\scripts\\smoke-local.ps1`) e consulte `artifacts/smoke/rc50_52_smoke_result.txt`. Para cache/build, execute `dotnet clean sigov.runtime.slnf` e restaure novamente; não apague dados nem manipule `schema_migrations`.

SQLSTATE comuns: `42P01` (tabela ausente), `42703` (coluna ausente), `42P17` (expressão inválida em índice), `23505` (unicidade) e `28P01` (credencial). Sempre identifique migration/stage e corrija a fonte; nunca marque manualmente como aplicada.

## Pré-demonstração
Execute os validadores, aplique migrations, faça build Release, confirme Swagger/health e percorra login, MinhaCentral, ProjectStatus, Observabilidade, Segurança, Auditoria e LGPD. Senhas de produção nunca devem ser documentadas ou gravadas nos artefatos.
