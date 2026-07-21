# Diagnóstico CI Pós-RC 20

## Workflow analisado

- Run informado: 273.
- Limitação local: o checkout entregue ao agente não possui remote configurado, e a imagem local não contém `dotnet`, `pwsh` ou PostgreSQL client/server. Assim, os logs reais ainda devem ser complementados pelos artifacts do GitHub Actions após o push desta branch.

## Falhas tratadas a partir do enunciado e inspeção do repositório

| Job | Step | Primeira mensagem real conhecida | Causa raiz | Correção aplicada | Resultado esperado |
|---|---|---|---|---|---|
| sql-validate | Apply migrations in order | `null value in column tenant_id` em `sigov.perfil_acesso` | migration `20260609090000` inseria perfil sem tenant e alternava `plataforma`/`plataforma-global` | tenant estrutural unificado em `plataforma-global`, `perfil_acesso.tenant_id` criado e preenchido explicitamente | migration não gera `tenant_id` nulo |
| standalone-postgres-runtime | Apply script_completop twice | segundo `psql` podia falhar e `tee` mascarava o exit code | ausência de `set -euo pipefail` no step | step passa a usar `set -euo pipefail` | falha real propaga exit code |
| script-completop-validate | Verify consolidated SQL | arquivo versionado divergia do gerador | gerador lia todas as migrations e incluía seed de desenvolvimento | gerador passa a usar `manifest.json`, checksums e exclusões explícitas | baseline determinística |
| release-package-check/go-live-check | versionamento | scripts ainda referenciavam `1.0.0-rc17` | versão corrente não centralizada | criado `eng/version.json` e referências correntes atualizadas para `1.0.0-rc20` | artefatos rc20 |
