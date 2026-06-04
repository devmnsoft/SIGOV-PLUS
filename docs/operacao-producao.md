# Operação em produção

Production exige secrets por variável de ambiente ou secret manager, JWT secret com tamanho mínimo, CORS restrito, seed demo desligado e Swagger desligado ou protegido.

Health checks disponíveis:

- `/api/health/live`
- `/api/health/ready`
- `/api/health/db`
- `/api/health/storage`
- `/api/health/outbox`
- `/api/health/version`
