# Backup e restore

Backups usam `pg_dump` em formato custom e verificação SHA-256.

```powershell
scripts/backup-db.ps1
scripts/verify-backup.ps1 -BackupFile backups/sigov-Development-YYYYMMDD-HHMMSS.dump
scripts/restore-db.ps1 -BackupFile backups/sigov-Development-YYYYMMDD-HHMMSS.dump
```

Restore em Production exige confirmação explícita `RESTORE_PRODUCTION_SIGOV`.
