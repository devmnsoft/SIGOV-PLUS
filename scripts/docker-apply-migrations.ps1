$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

docker compose up -d postgres
docker compose run --rm db-migrations
