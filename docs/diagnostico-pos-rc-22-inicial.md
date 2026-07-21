# Diagnóstico inicial Pós-RC 22

- Branch inicial do ambiente: `work`.
- SHA inicial: `cd8cb49` (`Merge pull request #124 from devmnsoft/codex/corrigir-integridade-do-runtime-e-ci`).
- Branch de trabalho solicitada: `codex/pos-rc-22-runtime-ci-operacional-verde`.
- Run de referência: 277.
- Jobs vermelhos informados: `build-test`, `sql-validate`, `script-completop-validate`, `smoke-static`, `standalone-postgres-runtime`, `docker-build` (Web), `docker-compose-e2e`, `release-package-check`.
- SDK .NET no container: indisponível (`dotnet: command not found`).
- PostgreSQL/psql no container: indisponível (`psql: command not found`).
- PowerShell no container: indisponível (`pwsh: command not found`).
- Docker no container: indisponível (`docker: command not found`).

## Correções aplicadas neste ciclo

- Removida a duplicidade de `MobileCampoController`, preservando a implementação operacional transversal conectada a `MobileCampoService`.
- Corrigido namespace de `IAuditTrailService` no `AuthController`.
- Ajustada herança dos view models administrativos removendo `sealed` das bases intencionalmente herdadas.
- Removidos atributos antiforgery duplicados em actions multi-rota.
- Corrigido `using Xunit` em teste estático.
- Criado helper `TestRepoPath` multiplataforma para testes de integração.
- Ajustada migration financeira para conflito canônico `(modulo, chave)` e slug `plataforma-global`.
