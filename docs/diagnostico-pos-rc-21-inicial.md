# Diagnóstico inicial Pós-RC 21

## Ambiente local

- SHA inicial: `9754e4774be62f198516c9dbfbcc430076cf39c6`
- Branch inicial disponível no ambiente: `work`
- Branch base `main`: indisponível no clone local fornecido.
- SDK .NET: comando `dotnet --version` indisponível no ambiente local (`dotnet: command not found`).
- Runtime .NET: indisponível pelo mesmo motivo.
- PowerShell: não validado inicialmente neste ambiente.
- PostgreSQL: não validado inicialmente neste ambiente.
- Docker: não validado inicialmente neste ambiente.
- Último workflow / jobs com falha: informados na solicitação como run 275, com falhas de build, SDK, migrations, script completo, schema migrations, manifest, PowerShell, bootstrap e núcleo operacional.

## Erros confirmados para estabilização inicial

1. `EnterpriseDapperCrudService.CodePrefix` usava construção alvo-ambígua `new(char[])`, incompatível com o compilador reportado.
2. Ausência de `global.json` permitia seleção silenciosa de SDK diferente do esperado pelo workflow.

## Limitação do ambiente de execução

Este ambiente não possui o CLI `dotnet`; portanto, os comandos de build/test/format foram registrados como bloqueados por dependência de ambiente local, sem desabilitar warnings, testes ou projetos.
