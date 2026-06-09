Write-Host "Verificando containers SIGOV..." -ForegroundColor Cyan
docker compose ps

Write-Host "Testando API Health..." -ForegroundColor Cyan
try {
    $api = Invoke-WebRequest -Uri "http://localhost:5001/api/health/live" -UseBasicParsing -TimeoutSec 10
    Write-Host "API Health OK: $($api.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "API Health falhou: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Testando Web..." -ForegroundColor Cyan
try {
    $web = Invoke-WebRequest -Uri "http://localhost:8080" -UseBasicParsing -TimeoutSec 10
    Write-Host "Web OK: $($web.StatusCode)" -ForegroundColor Green
} catch {
    Write-Host "Web falhou: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "Logs recentes da API:" -ForegroundColor Cyan
docker compose logs --tail=50 api

Write-Host "Logs recentes da Web:" -ForegroundColor Cyan
docker compose logs --tail=50 web

Write-Host "Logs recentes do Worker:" -ForegroundColor Cyan
docker compose logs --tail=50 worker

Write-Host "Endereços:" -ForegroundColor Yellow
Write-Host "Web: http://localhost:8080"
Write-Host "API: http://localhost:5001"
Write-Host "Health: http://localhost:5001/api/health/live"
