# Deploy produção SIGOV PLUS

Checklist técnico: PostgreSQL, connection string via secret, storage privado, SMTP, OCR, IA, workers, HTTPS, reverse proxy, logs estruturados, health checks, backup/restore testados, rollback, LGPD, monitoramento e rotação de secrets.

Variáveis típicas: `ASPNETCORE_ENVIRONMENT=Production`, connection string, chaves JWT/API, SMTP, storage, provider OCR, provider IA, limites mensais e URLs públicas.
