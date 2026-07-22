# Diagnóstico CI Pós-RC 25

Correções aplicadas para os primeiros erros conhecidos:

- Teste de DI recebeu namespaces reais para `ITenantResolver`, `IOutboxService`, `IHealthCheck` e `DefaultOutboxHandler`.
- Parser PowerShell do CI passou a usar `$parseTokens`, `$parseErrors` e `$parseError`.
- Artifacts de PowerShell padronizados: `powershell-parser.log`, `psscriptanalyzer.log`, `smoke-static.log`.
- Dockerfiles de produção deixaram de restaurar a solução e projetos de teste.
