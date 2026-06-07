# Changelog

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
