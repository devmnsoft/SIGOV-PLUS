# Checklist de go-live do sigov

## Validação obrigatória

1. Execute `scripts/validate.ps1` em ambiente com .NET 6 e Docker.
2. Execute `scripts/security-check.ps1` para validar Swagger, CORS, seed demo e ausência de segredo versionado em Production.
3. Execute `scripts/check-residues.ps1` para procurar resíduos de nomes antigos, tipos SQL Server e segredos em texto claro.
4. Execute `scripts/go-live-check.ps1` antes de gerar release candidate.

## Health endpoints

Validar os endpoints preservados:

- `/api/health`
- `/api/health/live`
- `/api/health/ready`
- `/api/health/db`
- `/api/health/outbox`
- `/api/health/storage`
- `/api/health/version`

## Critérios finais

- Build Release sem warnings.
- Testes unitários, integrados e API tests aprovados.
- Docker production com PostgreSQL sem porta pública.
- Secrets fornecidos por variáveis de ambiente ou secret manager.
- CORS Production com origens explícitas.
- Swagger Production desabilitado ou protegido.
