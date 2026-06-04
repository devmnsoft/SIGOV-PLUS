@echo off
echo Iniciando sigov...
docker compose up -d --build
echo Web: http://localhost:5000
echo API: http://localhost:5001
echo Swagger: http://localhost:5001/swagger
echo Health: http://localhost:5001/api/health
echo DB Health: http://localhost:5001/api/health/db
