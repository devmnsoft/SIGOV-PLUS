# Diagnóstico inicial — Pós-RC 28A

## Base auditada

- SHA esperado: `51d5475037cf01150010e141fda9833ec268d01a`.
- SHA local real antes das alterações: `51d5475037cf01150010e141fda9833ec268d01a`.
- Branch local recebida: `work`.
- Branch de trabalho: `codex/pos-rc-28a-net10-postgres-standalone`.

A tentativa obrigatória de `git fetch origin` foi executada antes de alterar arquivos. O clone não possuía remoto; após configurar a URL canônica, a rede do ambiente recusou o túnel HTTPS com HTTP 403. Como o SHA local coincide exatamente com o SHA base solicitado, ele foi adotado sem reescrever histórico. O workflow 316 e seus artefatos não puderam ser baixados pela mesma limitação externa; nenhuma evidência foi inventada.

## Inventário inicial

A solution contém 10 projetos: Domain, Application, Infrastructure, Api, Web, Worker, Testing, UnitTests, IntegrationTests e ApiTests. O framework era centralizado como `net6.0`, C# como 10.0 e o nível de análise como `latest`. Havia referências Microsoft 6.x, Npgsql 6.x, imagens Docker 6.0 e seis configurações de SDK 6.0.x nos workflows.

## Limitações verificáveis

O executável `dotnet` não existe na imagem de execução. Restore, build, testes, geração de lock files, format e auditoria NuGet devem ser executados por um runner com o SDK 10. Os portões posteriores permanecem deliberadamente não declarados como aprovados.
