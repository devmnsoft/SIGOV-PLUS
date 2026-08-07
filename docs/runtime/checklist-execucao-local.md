# Checklist de execução local

- [ ] PowerShell, .NET e PostgreSQL/psql 16+ instalados.
- [ ] `setup-local-sigov.ps1` terminou sem erro crítico.
- [ ] `.env.local` não foi versionado e usa `MigrationMode=Disabled`.
- [ ] `start-local.ps1` exibiu as URLs sem exibir segredo.
- [ ] `status-local.ps1` mostra banco, schema e runtime user OK.
- [ ] `check-runtime.ps1` retorna 0 (ou 1 apenas para avisos conhecidos).
- [ ] `/api/health/live`, `/api/health/ready` e `/` respondem.
- [ ] `stop-local.ps1` removeu os PIDs.
