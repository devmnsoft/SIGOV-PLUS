param([string]$OutputDir = "./backups")
New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$target = Join-Path $OutputDir ("sigov-" + (Get-Date -Format "yyyyMMdd-HHmmss") + ".dump")
pg_dump $env:SIGOV_POSTGRES_CONNECTION --format=custom --file=$target
Write-Host "Backup gerado em $target"
