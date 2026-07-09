# Pós-RC 07 — diagnostico pos rc 07

## Status
Pós-RC 07 preparada para validação em GitHub Actions/homologação, com bloqueios documentados quando ferramentas locais não existirem. Não declarar produção antes de build, testes, Docker E2E, seed idempotente, smoke autenticado, package sanitizado e go-live check passarem no ambiente alvo.

## Evidências executáveis
- Ambiente local padronizado para `POSTGRES_DB=sigov`, `POSTGRES_USER=sigov`, `POSTGRES_PASSWORD=change_me_local_only` e `POSTGRES_PORT=5432`.
- Dashboard e fluxos Web resolvem tenant por claim, header interno, configuração, domínio ou demo mode explícito; sem tenant específico, o dashboard opera como Admin Global agregado.
- CI contém build/test, Docker build, SQL validate, smoke static, Docker Compose E2E, release package check e go-live-check.
- Go-live check gera `docs/go-live-check-result.md` e `docs/go-live-check-result.json` com total, passed, warnings, failedBlocking, failedNonBlocking, statusFinal e releaseCandidateVersion.
- Seed demo mantém API key apenas em hash SHA-256 e tenant 1 é documentado como demo/homologação.

## Limitações honestas
- ICP-Brasil, Gov.br, OCR, SMTP, WhatsApp e entrega externa de webhooks permanecem dependentes de provider real.
- Fallback honesto deve indicar schema/provedor indisponível sem apresentar número fake.
- Evidências locais de dotnet/docker/pwsh dependem da disponibilidade das ferramentas no executor.

## Próximos passos
1. Executar pipeline no GitHub Actions.
2. Conferir artefatos do Docker Compose E2E, smoke, package release e go-live check.
3. Promover para homologação final somente se não houver bloqueio crítico.
