docker compose up -d

Write-Host "Aguardando API..." -ForegroundColor Cyan
Start-Sleep -Seconds 10

try {
    Invoke-WebRequest -Uri "http://localhost:5001/api/health/live" -UseBasicParsing -TimeoutSec 10 | Out-Null
    Write-Host "API disponível." -ForegroundColor Green
} catch {
    Write-Host "API ainda não respondeu: $($_.Exception.Message)" -ForegroundColor Yellow
}

Start-Process "http://localhost:8080"

Write-Host "Acesso SIGOV:" -ForegroundColor Yellow
Write-Host "Web: http://localhost:8080"
Write-Host "API: http://localhost:5001"
Write-Host "Health: http://localhost:5001/api/health/live"
Write-Host "Login: admin"
Write-Host "Senha: Admin@123"

# Evolução Pós-Build 03 - URLs SaaS e Tributário
$PostBuild03Urls = @(
    "http://localhost:8080/Saas/Planos",
    "http://localhost:8080/Saas/Implantacao",
    "http://localhost:8080/Saas/Parametros",
    "http://localhost:8080/Tributario/Dashboard",
    "http://localhost:8080/Tributario/Configuracao",
    "http://localhost:8080/Tributario/Contribuintes",
    "http://localhost:8080/Tributario/Imoveis",
    "http://localhost:8080/Tributario/Economicos"
)
$PostBuild03Urls | ForEach-Object { Write-Host "SIGOV Pós-Build 03: $_" }
