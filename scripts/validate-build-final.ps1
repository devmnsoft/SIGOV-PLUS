dotnet clean sigov.sln
dotnet restore sigov.sln
dotnet build sigov.sln
dotnet test sigov.sln
docker compose down
docker builder prune -f
docker compose build --no-cache
docker compose up -d
docker compose ps
docker compose logs --tail=100 db-migrations
docker compose logs --tail=100 postgres
docker compose logs --tail=100 api
docker compose logs --tail=100 web
docker compose logs --tail=100 worker
