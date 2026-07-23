# Diagnóstico do workflow Pós-RC 27A

Erros actionlint/ShellCheck iniciais registrados:

1. `SC2129` no bloco `release-context`, corrigido com redirecionamento agrupado para `$GITHUB_OUTPUT`.
2. `SC2034` no loop de inicialização standalone, corrigido com variável `attempt` utilizada no log.
3. `SC2034` no loop de health do Docker Compose, corrigido com variável `attempt` utilizada no log.

A auditoria passou a bloquear referências a `tests/Sigov.Tests/Sigov.Tests.csproj`, filtros sem testes correspondentes, jobs sem timeout, artifacts duplicados, scripts/csproj inexistentes e gates de Tarefas removidos para o Pós-RC 27B.
