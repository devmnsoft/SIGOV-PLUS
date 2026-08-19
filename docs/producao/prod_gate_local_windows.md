# Gate de produção local no Windows

## Pré-requisitos

Em PowerShell, abra `C:\MNSOFT\SIGOV-PLUS`, instale o SDK indicado por `global.json` e PostgreSQL client/server 16, e confirme `dotnet`, `psql`, `pg_dump`, `pg_restore`, `python` e Git Bash no `PATH`. O banco padrão é `postgres`, schema/search path `sigov`.

```powershell
Set-Location C:\MNSOFT\SIGOV-PLUS
$env:PGPASSWORD = Read-Host 'Senha PostgreSQL'
.\scripts\prod-gate-local.ps1
Remove-Item Env:PGPASSWORD
```

O script falha se faltar ferramenta, aplica migrations sem marcar/pular versões, compila Release com warnings como erros, inicia API/Web em HTTP, sonda health/Swagger/páginas, faz backup e restore no banco isolado `sigov_restore_local`, executa smoke e grava `artifacts/smoke/rc50_54_prod_gate_local_windows_result.txt`. Senha e connection string não são gravadas.

## Login real

O gate não contorna antiforgery. Com credenciais locais fornecidas fora do relatório, execute `scripts/check-local-login.ps1` para admin e superadmin e registre apenas PASS/FAIL sanitizado. Se o banco de restore já existir, use um banco isolado vazio autorizado; nunca restaure sobre `postgres`.
