param(
  [string]$ComposeService = "postgres",
  [string]$Database = "sigov",
  [string]$User = "sigov",
  [string]$SeedPath = "database/postgres/seeds/pos_rc_homologacao_demo.sql"
)
$ErrorActionPreference = "Stop"
if (-not (Test-Path $SeedPath)) { throw "Seed não encontrado: $SeedPath" }
$envName = $env:ASPNETCORE_ENVIRONMENT
if ($envName -and $envName -eq "Production") { throw "Seed demo bloqueado em Production." }
Write-Host "Aplicando seed demo Pós-RC 04 ($SeedPath) em ambiente $($envName ?? 'Development/Homologation')..."
if (Get-Command docker -ErrorAction SilentlyContinue) {
  $content = Get-Content $SeedPath -Raw
  $content | docker compose exec -T $ComposeService psql -U $User -d $Database -v ON_ERROR_STOP=1
} elseif (Get-Command psql -ErrorAction SilentlyContinue) {
  psql -U $User -d $Database -v ON_ERROR_STOP=1 -f $SeedPath
} else { throw "Nem docker nem psql disponíveis para aplicar o seed." }
Write-Host "Seed demo aplicado com sucesso e de forma idempotente."
