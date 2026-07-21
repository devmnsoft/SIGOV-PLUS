# Diagnóstico CI Pós-RC 22

Status local limitado por ausência das ferramentas obrigatórias no container (`dotnet`, `pwsh`, `psql` e `docker`). As correções foram guiadas pelos erros confirmados do run 277 e por inspeção estática com `rg`, `sed`, `python3` e `git`.

## Jobs do run 277 a validar após push

| Job | Correção relacionada |
| --- | --- |
| build-test | Controllers duplicados, namespace de auditoria, herança sealed, antiforgery e Xunit/TestRepoPath |
| sql-validate | Upsert canônico em `sigov.permissao` |
| script-completop-validate | Manifest atualizado para a migration financeira |
| smoke-static | Nenhuma alteração destrutiva em scripts nesta iteração |
| standalone-postgres-runtime | Correção do slug `plataforma-global` e tenant em perfis financeiros |
| docker-build Web | Correções de compilação Web |
| docker-compose-e2e | Dependente de build/runtime verdes |
| release-package-check | Dependente de scripts/ferramentas indisponíveis localmente |
