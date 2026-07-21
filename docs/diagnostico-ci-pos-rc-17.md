# Diagnóstico inicial Pós-RC 17

- SHA analisado: 906e6079ef96e787b173f9aefe2870606982f85d
- Data: 2026-07-21
- Workflow analisado: `.github/workflows/ci.yml`
- Run ID mais recente: indisponível localmente; acesso à API do GitHub bloqueado por proxy HTTP 403 no ambiente.

## Falhas reproduzidas/analisadas

| Job | Etapa | Arquivo | Erro real | Causa raiz | Correção proposta |
|---|---|---|---|---|---|
| build-test | `dotnet build` | `src/Sigov.Api/Program.cs`, `src/Sigov.Web/Program.cs` | registros Enterprise duplicados/contraditórios em relação a `AddInfrastructure` | Pós-RC 16 registrou `EnterpriseDapperCrudService` em dois pontos | centralizar registros no `Sigov.Infrastructure.DependencyInjection` |
| smoke-static | PowerShell inline | `.github/workflows/ci.yml`, `scripts/smoke-test-sigov.ps1` | referências Pós-RC 15 e interpolação literal no resumo | validação estática defasada e script misturava smoke runtime com contratos estáticos | criar `-StaticOnly`, validar sintaxe PS e atualizar Pós-RC 17 |
| release-package-check | validação de pacote | `scripts/package-release.ps1` | pacote com nomes `rc-final` e evidências Pós-RC 15 | scripts ainda apontavam para release anterior | padronizar `1.0.0-rc17` e evidências Pós-RC 17 |
| go-live-check | validação documental | `scripts/go-live-check.ps1` | checks Pós-RC 15 e `-AllowWarnings` no CI | go-live aceitava ressalvas e documentação antiga | exigir execução bloqueante e docs Pós-RC 17 |
