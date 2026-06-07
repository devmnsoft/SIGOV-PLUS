# Smoke tests - sigov v1.0.0

## Comando

```powershell
pwsh scripts/smoke-test.ps1 -ApiBaseUrl http://localhost:5001 -WebBaseUrl http://localhost:5000 -SkipLogin
```

## Parâmetros

- `ApiBaseUrl`.
- `WebBaseUrl`.
- `Username`.
- `Password` ou `SIGOV_SMOKE_PASSWORD`.
- `Tenant`.
- `SkipLogin`.
- `Environment`.

## Cobertura

Health live/ready/db/outbox/version, Swagger em Development, home web e endpoints opcionais de tenant, módulos, pessoas, auditoria, suporte e integrações quando disponíveis.
