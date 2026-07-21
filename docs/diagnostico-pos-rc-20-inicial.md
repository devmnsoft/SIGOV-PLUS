# Diagnóstico inicial Pós-RC 20

- SHA inicial: 6d0fba7d1edb19f1a8f0b88ef9c14990dbe8d751
- Data UTC: 2026-07-21T17:59:48Z
- Versão alvo: 1.0.0-rc20
- Workflow inicial informado: run 273
- Jobs com falha informados: build-test, sql-validate, smoke-static, script-completop-validate, standalone-postgres-runtime, docker-build, docker-compose-e2e, release-package-check.

## Arquivos afetados nesta estabilização

- .github/workflows/ci.yml
- database/postgres/migrations/manifest.json
- database/postgres/migrations/011_seed_sigov_dev.sql
- database/postgres/migrations/20260609090000_pos_build_dashboard_saas.sql
- database/postgres/seeds/development/sigov_dev_demo.sql
- scripts/generate-script-completop.ps1
- scripts/validate-script-completop.ps1
- scripts/create-initial-admin.ps1
- scripts/create-initial-admin.sh
- script_completop.sql
- eng/version.json
