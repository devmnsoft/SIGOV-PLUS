# Go-live - sigov v1.0.0

## Checklist executável

Use `scripts/go-live-check.ps1` para validar Production. O script emite PASS, WARN ou FAIL e retorna exit code diferente de zero em falhas.

## Itens críticos

- Ambiente Production.
- Connection string por secret.
- JWT secret forte.
- CORS sem wildcard.
- Swagger Production desabilitado/protegido.
- Seed demo e admin default desabilitados.
- HTTPS configurado.
- Backup e restore protegidos.
- Docker production válido.
- Health endpoints respondendo.
- Worker ativo e outbox sem dead-letter crítico.
