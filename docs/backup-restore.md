# Backup, restore e manutenção

Use os scripts em `scripts/`. Nunca exponha senha em tela ou log. Restore é operação destrutiva e deve exigir janela, aprovação, backup prévio e ambiente alvo conferido.

Exemplos:

```powershell
./scripts/backup-postgres.ps1
./scripts/check-backup.ps1
./scripts/restore-postgres.ps1 -BackupFile ./backups/sigov.dump -ConfirmRestore
./scripts/backup-storage.ps1
```
