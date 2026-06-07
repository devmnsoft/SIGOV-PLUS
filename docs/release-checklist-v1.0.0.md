# Checklist de release v1.0.0

## Pré-release

- [ ] `VERSION` contém `v1.0.0`.
- [ ] `CHANGELOG.md` atualizado.
- [ ] Release notes final criada.
- [ ] Sem secrets reais versionados.
- [ ] Sem schemas SQL fora de `sigov`.
- [ ] Restore de banco exige confirmação explícita.

## Validação técnica

- [ ] `dotnet restore sigov.sln`.
- [ ] `dotnet build sigov.sln --configuration Release`.
- [ ] `dotnet test sigov.sln --configuration Release`.
- [ ] `docker compose config`.
- [ ] `docker compose build`.
- [ ] `docker compose -f docker-compose.prod.yml config`.
- [ ] `docker compose -f docker-compose.prod.yml build`.
- [ ] `scripts/validate-release.ps1`.

## Homologação

- [ ] `scripts/prepare-homologation.ps1` executado fora de Production.
- [ ] Tenant de homologação criado/validado.
- [ ] Admin de homologação criado com senha temporária fora do repositório.
- [ ] Smoke tests aprovados.
- [ ] Health, DB, outbox e version aprovados.

## Go-live

- [ ] `scripts/go-live-check.ps1` sem FAIL.
- [ ] Backup recente gerado e checksum validado.
- [ ] `scripts/rollback-check.ps1` sem FAIL.
- [ ] Plano de rollback aprovado.
- [ ] Pós-deploy planejado.
