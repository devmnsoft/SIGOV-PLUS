# RC50.44 — Protocolo de execução Codex

## Sequência obrigatória
1. **Validar build/migrations:** preservar working tree, ler `AGENTS.md`, inventariar ferramentas e executar validações estáticas antes de editar.
2. **Corrigir P0/P1:** resolver causa real; não mascarar warning, não usar `NoWarn`, não remover funcionalidade.
3. **Implementar funcionalidade:** fluxo vertical persistido com SQL parametrizado, tenant e idempotência.
4. **Aplicar design:** somente após estabilização; responsividade, acessibilidade, estados vazio/loading/erro e feedback.
5. **Validar rotas/Swagger/login:** API, Web, menu, credenciais Development e ausência de exposição sensível.
6. **Atualizar relatório final:** arquivos, migrations/checksums, comandos exatos, resultados, riscos e pendências.

## Comandos padrão
```bash
git status
python -m json.tool database/postgres/migrations/manifest.json
./scripts/check-migration-partial-index-columns.sh database/postgres/migrations
rg '"""' src -g '*.cs'
rg "SELECT \\*" src database/postgres/migrations
bash scripts/check-api-route-conflicts.sh
dotnet build sigov.runtime.slnf --configuration Release --nologo -warnaserror
psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql
```

## Guardrails
- C# 10; não usar raw string literal, EF, `SELECT *` nem SQL concatenado. Identificadores dinâmicos exigem allowlist/validação anterior à interpolação.
- Não criar database `sigov`, não dropar tabela nem apagar dados. Banco é `postgres`; schema/search path é `sigov`.
- Preservar login, Swagger, manifest e scripts completos. Após SQL: atualizar checksum, regenerar ambos os scripts pelos utilitários oficiais e repetir validação.
- Em sprint funcional, entregar no mínimo estabilização, funcionalidade, regra/auditoria/LGPD e UX/relatório/menu.
- Antes da RC50.52 não criar projeto/classe de teste, mock ou fixture.

## Ambiente incompleto
Se `dotnet` ou `psql` não existir: **não declarar build/migration/runtime validados**; marcar explicitamente como pendente por limitação ambiental, executar todas as validações estáticas disponíveis e não afirmar fechamento ponta a ponta. Não substituir execução real por inferência.

## Relatório mínimo
Commit/branch; working tree inicial/final; quatro avanços; arquivos e rotas; migration/checksum; build/migration/Swagger/login com ✅/⚠️/❌; P0/P1/P2 remanescentes; próximo passo concreto.
