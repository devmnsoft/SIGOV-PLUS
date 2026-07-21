# Diagnóstico CI Pós-RC 23A

## Workflow inicial

- Run inicial informado: 279
- Base local: f889c7fc66a25779248ce882558a1587865bbc3a

## Observabilidade necessária

O CI deve publicar logs de build, testes, parser PowerShell, validação SQL, validação do script completo, runtime standalone, Docker, pacote e go-live mesmo em falha.

## Ambiente local desta correção

As validações completas ficaram bloqueadas no contêiner por ausência de ferramentas obrigatórias:

- `dotnet`
- `pwsh`
- `psql`
- `docker`

As correções foram aplicadas para os erros conhecidos e preparadas para execução no GitHub Actions.
