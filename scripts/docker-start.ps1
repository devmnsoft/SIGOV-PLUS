$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Host "Arquivo .env criado a partir de .env.example. Revise a senha antes de homologacao/producao."
}

docker compose up -d --build

Write-Host ""
Write-Host "SIGOV Web: http://localhost:$((Get-Content .env | Select-String '^APP_HTTP_PORT=' | ForEach-Object { $_.ToString().Split('=')[1] } | Select-Object -First 1) -as [string])"
Write-Host "Se APP_HTTP_PORT nao estiver definido, use http://localhost:8080"
Write-Host "API interna/publicada para dev: http://localhost:5001"
Write-Host "Logs: scripts/docker-logs.ps1"
