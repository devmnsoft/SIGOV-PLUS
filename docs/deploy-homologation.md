# Deploy de homologação - sigov v1.0.0

## Pré-requisitos

- Docker e Docker Compose.
- SDK .NET 6 para validações locais.
- PostgreSQL controlado pelo compose de desenvolvimento/homologação.

## Variáveis

- `ASPNETCORE_ENVIRONMENT=Homologation` ou `Development`.
- `SIGOV_HML_TENANT_SLUG`.
- `SIGOV_HML_ADMIN_EMAIL`.
- `SIGOV_HML_ADMIN_PASSWORD` fornecida fora do repositório.
- `SIGOV_HML_ENABLE_DEMO_DATA=true|false`.

## Execução

```powershell
pwsh scripts/prepare-homologation.ps1
pwsh scripts/smoke-test.ps1 -Environment Homologation -SkipLogin
```

## Segurança

O preparo de homologação falha quando `ASPNETCORE_ENVIRONMENT=Production`. Credenciais temporárias devem ser geradas no ambiente e trocadas antes de qualquer implantação real.
