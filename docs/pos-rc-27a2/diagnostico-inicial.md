# Diagnóstico inicial Pós-RC 27A.2

- SHA base esperado: `45a4a171e3190ffbd10da5512c33396fbcd3dad3`.
- Workflow referência: run id `30052753674`, run number `294`, resultado `failure`.
- Jobs verdes iniciais: `workflow-integrity`, `release-context`.
- Jobs vermelhos iniciais: `build-test`, `powershell-lint`, `migrations-manifest`, `script-completop-validate`.
- Jobs skipped iniciais: `dependency-injection`, `web-smoke`, `seed-idempotency`, `script-completop-idempotency`, `schema-equivalence`, `standalone-postgres-runtime`, `docker-build`, `docker-compose-e2e`, `ui-contrast`, `release-package-check`, `go-live-check`.

## Erros exatos registrados

1. `CS1503` em `WebRuntimeSmokeTests.cs`: overload de `Contain` com `StringComparison` não disponível na versão instalada do FluentAssertions.
2. `CS1503` em `WebRuntimeSmokeTests.cs`: overload de `NotContain` com `StringComparison` não disponível para `StackTrace` HTML.
3. `CS1503` em `WebRuntimeSmokeTests.cs`: overload de `NotContain` com `StringComparison` não disponível para `<html` CSS.
4. `CS1503` em `WebRuntimeSmokeTests.cs`: overload de `NotContain` com `StringComparison` não disponível para `StackTrace` CSS.
5. Parser PowerShell: string sem terminador causada por cercas Markdown com crase em string de aspas duplas.
6. Parser PowerShell: fechamento de subexpressão ausente causado pelo mesmo padrão de crases em string interpolada.
7. PostgreSQL: `invalid sslmode value: ""` quando `PGSSLMODE` era exportado vazio.
8. Baseline: gerador continha versão/data hardcoded de Pós-RC 23A e data corrente fixa histórica.
