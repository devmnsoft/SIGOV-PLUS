# Matriz de erros

| Área | Erro inicial | Correção |
|---|---|---|
| actionlint | Bootstrap por asset tarball dependia de checksum hardcoded e falhava antes da execução do actionlint. | Instalação por módulo Go fixado em `v1.7.7`, com log de instalação e `trap` de erro. |
| workflow-integrity | Validador exigia `ACTIONLINT_SHA256`. | Validador agora exige `ACTIONLINT_VERSION`, `go install`, `setup-go` por SHA e artifacts reais. |
| WebRuntimeSmokeTests | Referência a `Sigov.Infrastructure.Data` e `IDbConnectionFactory` inexistente. | Uso de `Sigov.Infrastructure.Persistence.Dapper`, `DapperContext` e `NpgsqlConnectionFactory`. |
| WebRuntimeSmokeTests | CSS validado por `File.Exists` em `AppContext.BaseDirectory`. | CSS validado por HTTP em `/css/site.css`, `/css/sigov-base.css` e `/css/sigov-tokens.css`. |
| TRX | Saídas globais podiam ser sobrescritas. | `validate-trx-results.py` aceita caminhos de saída por job. |
