# Diagnóstico inicial Pós-RC 23A

- SHA inicial: f889c7fc66a25779248ce882558a1587865bbc3a
- Branch inicial disponível no ambiente: `work` (sem remote local configurado)
- Workflow inicial informado: run 279
- SDK .NET: indisponível no contêiner (`dotnet: command not found`)
- PostgreSQL/psql: indisponível no contêiner (`psql: command not found`)
- PowerShell: indisponível no contêiner (`pwsh: command not found`)
- Docker: indisponível no contêiner (`docker: command not found`)

## Jobs vermelhos informados

- build-test
- sql-validate
- script-completop-validate
- smoke-static
- standalone-postgres-runtime
- docker-build
- docker-compose-e2e
- release-package-check

## Primeiros erros reais informados

- `OperationalDemoService`: variável local não utilizada.
- `ComercialController`: conversão indevida de `long` para `Guid?`.
- `EnterprisePagesControllers`: conversão indevida de `long` para `Guid?`.
- `PosRc02RealFlowStaticTests`: `TestRepoPath` não resolvido.
- `PosRc06StaticTests`: `TestRepoPath` não resolvido.
- Migration tributária: `tenant_id` nulo em `perfil_acesso`.
- `script_completop.sql`: bloco financeiro consolidado desatualizado com conflito duplicado em `permissao_modulo_chave_key`.
