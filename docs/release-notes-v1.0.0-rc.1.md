# Release notes - sigov v1.0.0-rc.1

Data do RC: 2026-06-07.

## Escopo

Este release candidate consolida QA funcional geral, regressão técnica e hardening incremental para o produto `sigov`, mantendo a stack ASP.NET Core/.NET 6, Dapper, PostgreSQL e schema único `sigov`.

## Itens entregues

- Endpoint `/api/health/version` com `application`, `version`, `commit`, `environment` e `buildDate`.
- Variáveis opcionais de release: `SIGOV_VERSION`, `SIGOV_COMMIT_SHA`, `SIGOV_BUILD_DATE`.
- Matriz técnica em `docs/modulos-rotas-validacao.md` validada por script.
- Checklist de QA funcional em `docs/qa-funcional-release-candidate.md`.
- Scripts adicionais de validação: `scripts/check-module-map.ps1` e `scripts/check-web-assets.ps1`.
- Testes de regressão adicionados para API, migrations, tenant isolation, LGPD e worker/outbox.

## Pendências reais

- Ampliar execução E2E com banco PostgreSQL real para todos os fluxos funcionais.
- Implementar testes HTTP autenticados completos para perfis/permissões reais.
- Validar ambiente Docker em máquina com Docker disponível.
- Consolidar módulos estruturais/parciais de Compras, Tributário, Relatórios/BI e Suporte em releases futuras.

## Critérios para promoção à v1.0.0 final

1. `dotnet restore sigov.sln` aprovado em ambiente com SDK .NET 6.
2. `dotnet build sigov.sln` aprovado sem warnings.
3. `dotnet test sigov.sln` aprovado.
4. `docker compose config` e `docker compose build` aprovados.
5. Health endpoints validados em ambiente local/homologação.
6. Migrations aplicadas em banco limpo.
7. Scripts de resíduos, segurança, web assets, matriz e go-live aprovados.
