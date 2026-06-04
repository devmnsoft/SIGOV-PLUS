Write-Host "Resetando banco sigov..."
docker compose down -v
docker compose up -d --build sigov-postgres sigov-api
Write-Host "DB Health: http://localhost:5001/api/health/db"
