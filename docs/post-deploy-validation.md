# Pós-deploy - sigov v1.0.0

## Health

Validar:

- `/api/health`.
- `/api/health/live`.
- `/api/health/ready`.
- `/api/health/db`.
- `/api/health/outbox`.
- `/api/health/version`.

## Funcional

- Login administrativo com credencial real do cliente.
- Tenant correto e isolamento entre tenants.
- Módulos contratados.
- Fluxo mínimo de pessoa/core.
- Máscara LGPD em dados pessoais.
- Auditoria de alteração operacional.
- Worker processando outbox.

## Operação

- Logs sem dados sensíveis.
- Backup agendado.
- Métricas do banco e filas.
