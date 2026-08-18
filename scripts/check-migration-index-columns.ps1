param([string]$MigrationsPath = (Join-Path (Split-Path $PSScriptRoot -Parent) 'database/postgres/migrations'))
$ErrorActionPreference = 'Stop'
$checker = Join-Path $PSScriptRoot 'check-migration-index-columns.sh'
if (Get-Command bash -ErrorAction SilentlyContinue) { & bash $checker $MigrationsPath; exit $LASTEXITCODE }
throw 'bash não está disponível; execute o validador em WSL ou Git Bash.'
