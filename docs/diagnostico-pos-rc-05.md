# Diagnóstico Pós-RC 05 — hardening CI/CD, Docker, smoke e Go-Live

Data: 2026-07-07.

## Sincronização executada

- `git checkout main`: não executado com sucesso neste container porque não existe branch local/remota `main`; o repositório veio apenas com a branch `work`.
- `git pull`: não executado porque não há remoto configurado no clone local.
- Branch de trabalho criada localmente: `codex/pos-rc-05-hardening-ci-go-live`.

## Projetos existentes

A solução `sigov.sln` contém os projetos de produção `Sigov.Domain`, `Sigov.Application`, `Sigov.Infrastructure`, `Sigov.Api`, `Sigov.Web` e `Sigov.Worker`, além das suítes `Sigov.UnitTests`, `Sigov.IntegrationTests` e `Sigov.ApiTests`.

## Workflows existentes

- `.github/workflows/ci.yml`: consolidado nesta sprint com jobs separados para build/test, docker-build, sql-validate e smoke-static.
- `.github/workflows/release.yml`: workflow de release já existente mantido.

## Scripts existentes relevantes

- `scripts/apply-demo-seed.ps1`
- `scripts/smoke-test-sigov.ps1`
- `scripts/validate-release.ps1`
- `scripts/package-release.ps1`
- `scripts/schema-report.ps1`
- `scripts/go-live-check.ps1`
- Scripts auxiliares de Docker, backup, restore, validação e relatórios de schema.

## Migrations existentes

Foram identificadas migrations PostgreSQL em `database/postgres/migrations`, incluindo fundação de schema, infraestrutura, core, segurança/auditoria/LGPD, BI/workflow, módulos setoriais, SaaS, integrações/outbox/webhooks, protocolo/GED/workflow/API/outbox e consolidação Pós-RC.

## Seeds existentes

- `database/postgres/seeds/seed_sigov_dev.sql`
- `database/postgres/seeds/pos_rc_homologacao_demo.sql`

## Docker inspecionado

- `docker-compose.yml` define PostgreSQL, `db-migrations`, API, Worker e Web com healthchecks, volumes para PostgreSQL/storage e dependências por saúde/conclusão.
- Dockerfiles de API, Web e Worker usam .NET 6, restore por solução e publish Release.

## Limitações do ambiente do agente

As ferramentas abaixo não estão instaladas no container atual, portanto a validação local real não pôde ser concluída aqui:

- `dotnet --info`: `/bin/bash: dotnet: command not found`.
- `docker --version`: `/bin/bash: docker: command not found`.
- `pwsh`: `/bin/bash: pwsh: command not found`.

## Principais riscos encontrados

1. Build e testes dependem do GitHub Actions ou ambiente de homologação com SDK .NET 6 instalado.
2. Docker real depende de host com Docker Compose disponível; este container não permite validar runtime local.
3. Smoke autenticado da API depende de `SIGOV_SMOKE_API_KEY` e `SIGOV_SMOKE_TENANT_ID`; sem essas variáveis, os checks autenticados são registrados como não bloqueantes e não imprimem chave completa.
4. Webhooks externos, ICP/Gov.br, OCR, SMTP, WhatsApp e integrações oficiais continuam dependentes de provedores externos.
5. A classificação final deve permanecer honesta: itens não executados neste ambiente não foram declarados como “passaram localmente”.
