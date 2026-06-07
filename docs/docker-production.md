# Docker Production

## Compose

Use `docker compose -f docker-compose.prod.yml config` para validar a configuração. O PostgreSQL fica somente na rede interna e não expõe porta pública por padrão.

## Imagens

API, Web e Worker usam Dockerfile multi-stage e usuário não-root. Logs seguem para stdout/stderr.

## Secrets

Use `.env.production.example` apenas como contrato de variáveis. Substitua placeholders por secret manager ou variáveis seguras no ambiente de deploy.
