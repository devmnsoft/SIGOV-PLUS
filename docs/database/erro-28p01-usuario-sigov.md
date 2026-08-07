# PostgreSQL 28P01 para o usuário `sigov`

O erro significa que a senha enviada pela aplicação não corresponde ao papel PostgreSQL. Confirme host, porta, banco e usuário em `.env.local`, sem imprimir a senha, e sincronize:

```powershell
$env:PGPASSWORD='<senha administrativa em secret seguro>'
./scripts/provision-sigov-db-user.ps1 -Database sigov -AdminUser postgres -AppDbUser sigov
./scripts/status-local.ps1
```

O provisionamento é idempotente, mantém o papel como `NOSUPERUSER`, aplica apenas conexão/uso/DML/sequências no schema `sigov` e testa o próprio usuário runtime. Não resolva concedendo superuser e não registre a connection string em logs.
