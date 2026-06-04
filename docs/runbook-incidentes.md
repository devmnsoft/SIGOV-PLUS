# Runbook de incidentes

1. Verificar `/api/health/ready` e `/api/health/db`.
2. Conferir logs por `CorrelationId` e `TenantId`.
3. Validar status do tenant no SaaS Admin.
4. Suspender tenant, quando necessário, via endpoint administrativo.
5. Verificar backups com SHA-256 antes de qualquer restore.
