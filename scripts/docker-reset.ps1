$ErrorActionPreference = "Stop"
$Root = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $Root

$confirmation = Read-Host "Digite RESETAR BANCO para apagar volumes Docker de banco/storage e recriar o ambiente"
if ($confirmation -ne "RESETAR BANCO") {
    Write-Host "Reset cancelado. Nenhum volume foi removido."
    exit 0
}

docker compose down -v
docker compose up -d --build
