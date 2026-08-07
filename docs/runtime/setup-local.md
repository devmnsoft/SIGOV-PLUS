# Setup e execução local

## Fluxo recomendado

Pré-requisitos: PowerShell 7, .NET SDK, `psql` 16+ e PostgreSQL 16 acessível. Execute:

```powershell
./scripts/setup-local-sigov.ps1 -AdminPassword $env:PGPASSWORD
./scripts/start-local.ps1
./scripts/status-local.ps1
./scripts/check-runtime.ps1
./scripts/stop-local.ps1
```

O setup cria/preserva `.env.local`, gera segredos locais quando ausentes, instala o banco, provisiona o papel `sigov` sem privilégios administrativos, valida e compila. Valores secretos ficam somente no ambiente local e nunca nos relatórios. Use `-Force` para sincronizar novamente o arquivo e `-SkipDatabaseInstall` quando o banco veio de `script_completop.sql`.

`script_completop.sql` contém o banco; `install-sigov-database.ps1` o instala e inicializa; `provision-sigov-db-user.ps1` sincroniza o login runtime e suas permissões; `start-local.ps1` valida e inicia os processos.

## Migrations

Use `SIGOV_MIGRATION_MODE=Disabled` e `SIGOV_RUN_MIGRATIONS=false` para banco preparado pelo instalador. `ValidateOnly` verifica o manifest sem aplicar. `ApplyPending` só deve ser usado deliberadamente em janela controlada; em produção, forneça a configuração explicitamente.
