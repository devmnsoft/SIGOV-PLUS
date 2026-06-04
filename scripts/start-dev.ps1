Write-Host "Iniciando sigov..."
docker compose up -d --build
Write-Host "Web: http://localhost:5000"
Write-Host "API: http://localhost:5001"
Write-Host "Swagger: http://localhost:5001/swagger"
Write-Host "Health: http://localhost:5001/api/health"
Write-Host "DB Health: http://localhost:5001/api/health/db"
