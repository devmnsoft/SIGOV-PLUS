param([string]$StoragePath = "./storage", [string]$OutputDir = "./backups")
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
Compress-Archive -Path $StoragePath -DestinationPath (Join-Path $OutputDir ("storage-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".zip"))
