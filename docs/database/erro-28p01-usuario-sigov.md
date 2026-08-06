# Correção do erro 28P01 no usuário `sigov`

## Sintoma

Ao iniciar a API, Worker ou Web, o runtime pode parar com:

```text
Npgsql.PostgresException: 28P01: autenticação do tipo senha falhou para o usuário "sigov"
```

Esse erro acontece antes de qualquer migration SQL ser aplicada. Ele indica que a senha usada pela aplicação em `ConnectionStrings__DefaultConnection`, `appsettings.json` ou `.env.local` não bate com a senha real do papel PostgreSQL `sigov`.

## Correção recomendada em ambiente local

Escolha uma senha local e use a mesma senha no banco e no `.env.local`.

```powershell
$env:PGPASSWORD = 'SENHA_DO_POSTGRES_ADMIN'
$env:SIGOV_DB_PASSWORD = 'change_me'

./scripts/provision-sigov-db-user.ps1 `
  -HostName localhost `
  -Port 5432 `
  -Database sigov `
  -MaintenanceDatabase postgres `
  -AdminUser postgres `
  -AppDbUser sigov
```

Depois confirme o `.env.local`:

```text
SIGOV_DB_HOST=localhost
SIGOV_DB_PORT=5432
SIGOV_DB_NAME=sigov
SIGOV_DB_USER=sigov
SIGOV_DB_PASSWORD=change_me
SIGOV_RUN_MIGRATIONS=false
SIGOV_MIGRATION_MODE=Disabled
```

## Quando o `script_completop.sql` já foi executado

Não é necessário recriar o banco. Execute apenas o provisionamento do usuário runtime:

```powershell
$env:PGPASSWORD = 'SENHA_DO_POSTGRES_ADMIN'
$env:SIGOV_DB_PASSWORD = 'A_MESMA_SENHA_DO_ENV_LOCAL'
./scripts/provision-sigov-db-user.ps1 -Database sigov -AdminUser postgres -AppDbUser sigov
```

O script faz quatro coisas de forma idempotente:

1. cria o papel PostgreSQL `sigov`, caso ele não exista;
2. atualiza a senha do papel `sigov` para a senha informada;
3. concede permissões de conexão e uso do schema `sigov`;
4. testa a conexão usando o próprio usuário `sigov`.

## Observação sobre migrations no runtime local

Quando o banco já foi criado pelo `script_completop.sql` ou por `install-sigov-database.ps1`, o runtime local deve iniciar com:

```text
SIGOV_RUN_MIGRATIONS=false
SIGOV_MIGRATION_MODE=Disabled
```

Assim a aplicação não tenta reaplicar migrations ao iniciar. Para validar migrations sem aplicar nada, use explicitamente:

```text
SIGOV_MIGRATION_MODE=ValidateOnly
```

Para aplicar migrations no startup em ambiente controlado:

```text
SIGOV_MIGRATION_MODE=ApplyPending
```

Não use `ApplyPending` em produção sem usuário de banco e política de migration definidos para o ambiente.
