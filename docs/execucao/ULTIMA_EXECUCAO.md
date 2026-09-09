# Última execução

Data: 2026-09-08. RC P0-GOV-20260908. Estado: EM_EXECUCAO; P0 não aprovado.

## Git e preservação

- Repositório: C:/MNSOFT/SIGOV-PLUS. Seis projetos src, database/postgres/migrations e tests confirmados.
- Inicial: codex/evolucao-saas-industria-360, HEAD e5c7e6c5ef782b38c17a6c3822c779e67ec9b3a9, sem upstream.
- Remoto: https://github.com/devmnsoft/SIGOV-PLUS.git.
- Commit local e5c7e6c sem mesmo SHA remoto; árvore idêntica à publicação 9f6618d, incorporada na main pelo PR #376.
- Fetch seguro: origin/main avançou para 5ef7516c; nenhuma diferença de árvore em relação ao checkout inicial.
- Branch desta fase: codex/p0-governanca-migrations, criada de origin/main (tracking inicial origin/main).
- Estado inicial: sete appsettings modificados; backups SQL, .vs, bin/obj, .github/copilot-instructions.md, migration mobile e lock de integração não rastreados. São preexistentes e não pertencem a esta fase.
- Branches disponíveis inspecionadas com git branch -avv; main e branch anterior locais, além das branches remotas históricas. Inventário exato será registrado junto às evidências.

## Achados iniciais

184 SQLs e 174 entradas no manifesto; dez órfãs; zero versões duplicadas no manifesto; um prefixo incompatível. Histórico usa knownChecksums com pós-condições.
sigov.sln contém somente runtime: dotnet test nessa solução não é evidência de testes executados. Swagger já possui teste runtime, que será reaproveitado.

## Ambiente

SDK efetivo 10.0.400 por rollForward latestFeature; 10.0.100 não instalado. PostgreSQL local 18, sem PostgreSQL 16; Docker CLI existe, daemon ausente. ConnectionStrings__DefaultConnection e senhas PostgreSQL não fornecidas ao processo.

BLOCKED: banco vazio/legado PostgreSQL 16 não executado porque não há instância descartável 16 disponível.
BLOCKED: login/isolamento runtime não executados porque dependem de banco validado e contas de teste.

## Escopo ativo

Governança de migrations, build e testes existentes. P1/P2/P3/P4 e GED não iniciados. Nenhuma migration histórica será alterada.

