# Deploy Production - sigov v1.0.0

## Pré-requisitos

- Imagem Docker buildada a partir da tag/revisão aprovada.
- PostgreSQL com schema único `sigov`.
- Secret manager ou variáveis seguras para connection string, JWT e senha do banco.
- Backup recente com checksum antes da janela.

## Variáveis obrigatórias

- `ASPNETCORE_ENVIRONMENT=Production`.
- `ConnectionStrings__DefaultConnection`.
- `Sigov__Jwt__Secret` com pelo menos 32 caracteres.
- `Sigov__Security__CorsAllowedOrigins__0` com origem HTTPS explícita.
- `Sigov__Security__SwaggerEnabledInProduction=false` por padrão.
- `Sigov__Seed__Demo=false`.
- `POSTGRES_PASSWORD` via secret.

## Validação antes do deploy

```powershell
pwsh scripts/go-live-check.ps1 -AllowWarnings
pwsh scripts/rollback-check.ps1 -AllowWarnings
```

## Aplicação

1. Gerar backup e checksum.
2. Validar pacote com `release-manifest.json`.
3. Subir serviços com `docker compose -f docker-compose.prod.yml up -d --build` ou orquestrador equivalente.
4. Validar health endpoints.
5. Validar Worker/outbox e logs.

## Restore

Use `scripts/restore-db.ps1` somente com confirmação explícita documentada na janela. Não execute restore como parte do rollback check.
