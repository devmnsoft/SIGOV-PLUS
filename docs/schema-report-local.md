# Schema report local

Não gerado neste ambiente de execução porque `docker` não está instalado no container do agente.

Para gerar evidência real no ambiente Docker local:

```powershell
./scripts/schema-report.ps1
```

O script executa `database/diagnostics/schema-report.sql` no container PostgreSQL e grava este arquivo com o resultado real de `information_schema.columns`.
