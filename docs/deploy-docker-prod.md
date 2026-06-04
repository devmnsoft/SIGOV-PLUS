# Deploy Docker Production

Use `.env.production` baseado em `.env.production.example`, sem secrets versionados.

```bash
docker compose -f docker-compose.prod.yml config
docker compose -f docker-compose.prod.yml build
docker compose -f docker-compose.prod.yml up -d
```

O PostgreSQL fica apenas na rede interna e a aplicação é publicada via reverse proxy Nginx.
