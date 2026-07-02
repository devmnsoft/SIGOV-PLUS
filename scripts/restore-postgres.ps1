param([Parameter(Mandatory=$true)][string]$BackupFile,[switch]$ConfirmRestore)
if (-not $ConfirmRestore) { throw "Restore é destrutivo. Reexecute com -ConfirmRestore após validar ambiente e backup." }
pg_restore --clean --if-exists --dbname=$env:SIGOV_POSTGRES_CONNECTION $BackupFile
