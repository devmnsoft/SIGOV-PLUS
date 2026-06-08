$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Host "Arquivo .env criado a partir de .env.example."
}

Write-Host "Validando docker compose config..."
docker compose config | Out-Null

Write-Host "Buildando imagens..."
docker compose build

Write-Host "Subindo ambiente..."
docker compose up -d

Write-Host "Aguardando health do PostgreSQL e Web..."
$deadline = (Get-Date).AddMinutes(5)
do {
    $postgresHealth = docker inspect --format='{{.State.Health.Status}}' sigov-postgres 2>$null
    $webHealth = docker inspect --format='{{.State.Health.Status}}' sigov-web 2>$null
    if ($postgresHealth -eq "healthy" -and $webHealth -eq "healthy") { break }
    Start-Sleep -Seconds 5
} while ((Get-Date) -lt $deadline)

if ($postgresHealth -ne "healthy") { throw "PostgreSQL nao ficou healthy. Estado: $postgresHealth" }
if ($webHealth -ne "healthy") { throw "Web nao ficou healthy. Estado: $webHealth" }

Write-Host "Validando migrations..."
docker logs sigov-db-migrations | Select-String "Migrations aplicadas com sucesso" | Out-Null

Write-Host "Validando schema sigov no PostgreSQL..."
$db = (docker compose exec -T postgres printenv POSTGRES_DB).Trim()
$user = (docker compose exec -T postgres printenv POSTGRES_USER).Trim()
docker exec sigov-postgres psql -U $user -d $db -c "select schema_name from information_schema.schemata where schema_name = 'sigov';"

Write-Host "Ambiente Docker SIGOV validado com sucesso."
