param([string]$BackupDir = "./backups")
Get-ChildItem $BackupDir -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending | Select-Object -First 10 Name,Length,LastWriteTime
