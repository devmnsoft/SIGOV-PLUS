# Backup, restore e manutenção

Use os scripts em `scripts/`. Nunca exponha senha em tela ou log. Restore é operação destrutiva e deve exigir janela, aprovação, backup prévio e ambiente alvo conferido.

Exemplos:

```powershell
./scripts/backup-postgres.ps1
./scripts/check-backup.ps1
./scripts/restore-postgres.ps1 -BackupFile ./backups/sigov.dump -ConfirmRestore
./scripts/backup-storage.ps1
```

## Complemento Release Candidate 1.0.0-rc.2

Scripts validados para o roteiro documental:

- `scripts/backup-postgres.ps1`: backup lógico do PostgreSQL.
- `scripts/restore-postgres.ps1`: restore em homologação/recuperação.
- `scripts/backup-storage.ps1`: cópia do storage local/anexos.
- `scripts/check-backup.ps1`: verificação mínima de existência, tamanho e data.

Regras de homologação: testar restauração em ambiente separado, proteger arquivos com dados pessoais, nunca anexar dumps reais em issues/PRs e registrar evidência de restore antes do aceite.
