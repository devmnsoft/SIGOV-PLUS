param(
  [string]$Environment = $env:ASPNETCORE_ENVIRONMENT,
  [string]$ConnectionString = $env:ConnectionStrings__DefaultConnection,
  [string]$OutputDirectory = "backups"
)
$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($Environment)) { $Environment = "Development" }
if ([string]::IsNullOrWhiteSpace($ConnectionString)) { throw "ConnectionStrings__DefaultConnection obrigatório." }
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$file = Join-Path $OutputDirectory "sigov-$Environment-$timestamp.dump"
pg_dump --format=custom --no-owner --no-privileges --dbname=$ConnectionString --file=$file
$hash = (Get-FileHash -Algorithm SHA256 -Path $file).Hash
Write-Host "Backup sigov concluído: $file"
Write-Host "SHA256: $hash"
