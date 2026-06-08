$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$db = (docker compose exec -T postgres printenv POSTGRES_DB).Trim()
$user = (docker compose exec -T postgres printenv POSTGRES_USER).Trim()
docker compose exec postgres psql -U $user -d $db
