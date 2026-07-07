# Release notes - sigov v1.0.0

Data: 2026-06-07.

## Escopo

A versão `v1.0.0` promove o release candidate para entrega final homologável e implantável do produto `sigov`, mantendo ASP.NET Core/.NET 6, Dapper, PostgreSQL, schema físico único `sigov`, SaaS multi-tenant, auditoria, LGPD, Docker, Worker e outbox.

## Entregas principais

- `VERSION` definido como `v1.0.0`.
- `/api/health/version` com `application`, `version`, `releaseChannel`, `commitSha`, `buildDate`, `environment`, `database` e `schema`.
- Scripts finais: validação de release, smoke tests, homologação, go-live, rollback e pacote.
- `docker-compose.prod.yml` validável sem secrets reais versionados.
- Workflow manual de release com upload do pacote gerado.
- Documentação operacional para homologação, produção, smoke tests, rollback e pós-deploy.
- Testes de release cobrindo versionamento, go-live, homologação, package manifest e health version.

## Correções desde o RC

- Contrato final de versionamento e metadados de build.
- Go-live check com PASS/WARN/FAIL e exit code.
- Rollback check com pré-condições obrigatórias e restore protegido.
- Package release sem credenciais reais e com checksums.
- Bloqueio explícito de preparação de homologação em Production.

## Evidências esperadas de homologação

1. `dotnet restore sigov.sln`.
2. `dotnet build sigov.sln`.
3. `dotnet test sigov.sln`.
4. `scripts/validate-release.ps1`.
5. `scripts/smoke-test.ps1` com ambiente em execução.
6. `scripts/go-live-check.ps1` com variáveis Production reais via secret manager.
7. `scripts/rollback-check.ps1` com backup e checksum reais da janela.
8. `scripts/package-release.ps1` gerando `release-manifest.json`.
9. `docker compose config/build` e `docker compose -f docker-compose.prod.yml config/build`.

## Limitações conhecidas

- Homologação assistida com usuário final ainda deve produzir evidências funcionais assinadas.
- Testes E2E autenticados completos por perfil continuam no backlog pós-release.
- Módulos estruturais/parciais permanecem documentados para evolução controlada.

## Pós-RC 05 — hardening CI/CD e Go-Live

- CI consolidado com jobs independentes para build/test, Docker, validação SQL e smoke estático.
- Smoke E2E ampliado para rotas principais Web e API v1, com Markdown/JSON, tempos de resposta, bloqueante/não bloqueante e mascaramento de chave.
- Scripts de release ajustados para gerar pacote `sigov-plus-1.0.0-rc-final` sem secrets.
- Documentação de diagnóstico, testes manuais, segurança/LGPD e performance básica adicionada.
- Limitação registrada: validação local real de .NET/Docker/PowerShell depende de ambiente com essas ferramentas instaladas.
