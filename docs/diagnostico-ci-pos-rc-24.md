# Diagnóstico CI Pós-RC 24

Jobs obrigatórios esperados: build-test, powershell-lint, migrations-manifest, script-completop-validate, script-completop-idempotency, schema-equivalence, standalone-postgres-runtime, docker-build, docker-compose-e2e, ui-contrast, ui-smoke, tarefas-e2e, release-package-check e go-live-check.

Correções priorizadas neste lote:
- projetos de teste com `IsTestProject=true`;
- global usings xUnit padronizados;
- runner de migrations sem interpolação de connection string sensível;
- logs de migration com metadados sanitizados;
- status DELEGADA removido do fluxo de status de tarefas.
