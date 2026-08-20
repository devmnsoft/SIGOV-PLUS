# Changelog

## RC50.68E-R2 — 2026-08-20

- repetidos os gates obrigatórios sobre o merge do PR #270, registrando exit 127 para .NET,
  PostgreSQL/psql, PowerShell e actionlint ausentes;
- confirmados Ruby/YAML, integridade do workflow principal, JSON, shell, rotas e igualdade byte a
  byte dos scripts consolidados como verificações estáticas auxiliares;
- mantida a promoção **BLOCKED** porque o banco real, o build e os workflows com
  `SIGOV_CI_DB_PASSWORD` não puderam ser executados; RC50.69 permanece não iniciada.

## RC50.68E — 2026-08-20

- auditada a base do control plane SuperAdmin 360 sobre o merge do PR #269;
- consolidados os scripts PostgreSQL a partir do manifest e ampliada a verificação do gerador para
  todas as cópias distribuíveis de produção e desenvolvimento;
- corrigido o Plano Mestre para separar implementação entregue de promoção produtiva;
- registrados como BLOCKED os gates dependentes de .NET 10, PostgreSQL/psql, PowerShell,
  actionlint e execução autenticada dos workflows; RC50.69 não foi iniciada.

## v1.0.0 - 2026-06-07

### Adicionado
- Versionamento final `v1.0.0` com `VERSION`, release notes final, checklist de release e documentação operacional mínima.
- Metadados de build em `/api/health/version` por `SIGOV_VERSION`, `SIGOV_COMMIT_SHA`, `SIGOV_BUILD_DATE` e `SIGOV_RELEASE_CHANNEL`, mantendo `application`, `database` e `schema` como `sigov`.
- Scripts executáveis de validação final, smoke test, homologação, go-live, rollback e empacotamento de release.
- Pacote local de release em `artifacts/release/v1.0.0/` gerado por script com manifest, migrations, projetos e checksums.
- Workflow manual `.github/workflows/release.yml` para preparar artefato de release sem publicar imagens sem secrets configurados.
- Testes de release para metadados, checklist de go-live, homologação segura, pacote e health version.

### Corrigido
- Endpoint de versão passou a retornar contrato final com `releaseChannel`, `commitSha`, `buildDate`, `environment`, `database` e `schema`.
- Checklist de go-live deixou de ser apenas informativo e passou a retornar PASS/WARN/FAIL com exit code em falhas.
- Rollback passou a validar backup, checksum, versões, restore protegido e plano documentado sem executar restore.

### Segurança operacional
- Homologação bloqueada em `Production` por script e validador de aplicação.
- Swagger Production permanece desabilitado por padrão e, quando habilitado, exige proteção explícita já validada por options.
- CORS wildcard, seed demo, admin default e adapters dev são bloqueados/validados para Production.
- Pacote de release não inclui dumps reais, certificados, tokens ou secrets.

### Pendências pós-release
- Executar homologação assistida com banco real e evidências assinadas pelo cliente.
- Ampliar testes E2E autenticados completos por perfil funcional em releases futuras.
- Evoluir módulos estruturais/parciais em backlog próprio, sem bloquear o pacote v1.0.0 homologável.

## v1.0.0-rc.1 - 2026-06-07

### Adicionado
- Endpoint `/api/health/version` consolidado com metadados opcionais `SIGOV_VERSION`, `SIGOV_COMMIT_SHA` e `SIGOV_BUILD_DATE`.
- Matriz técnica de módulos, rotas, views, JavaScript, migrations, permissões, dashboards, exportações, testes e pendências.
- Scripts de validação `scripts/check-module-map.ps1` e `scripts/check-web-assets.ps1` para QA de matriz e assets MVC/Razor.
- Testes de regressão para contrato de API, versão, permissões por serviço, módulos/feature flags, migrations, tenant isolation, LGPD e worker/outbox.

### Corrigido
- Resposta de versão agora expõe `application = sigov`, versão de release candidate e metadados de build sem depender de secrets versionados.
- Cobertura estática de migrations reforçada para impedir schemas físicos fora de `sigov` e tipos SQL Server.
- Cobertura de worker/outbox reforçada para retry, dead-letter, tenant_id e logs correlacionáveis.

### Observações
- Docker e .NET devem ser validados em ambiente com SDK .NET 6 e Docker instalados.
- Módulos estruturais/parciais foram registrados como pendência real, sem criação de módulos novos neste RC.
