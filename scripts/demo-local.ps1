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
