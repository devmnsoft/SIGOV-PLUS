# RC50.96 — correção de build e base PostgreSQL

## Entrega

- Corrigida a expressão Razor de exportação Saúde360 que fazia `.csv` ser interpretado como membro de `string`.
- Reforçada a neutralização de fórmulas no CSV de Saneamento360, incluindo tabulação e retorno de carro.
- Mantido o baseline completo e idempotente da RC50.95; adicionados scripts portáveis de restauração e documentação inequívoca para `psql` e `pg_restore`.
- Confirmada a sincronização da migration publicada, manifest e scripts completos, sem alteração destrutiva de schema.

## Validação e impedimentos

A inspeção estática cobriu links CSV Razor, formulários, marcadores de implementação artificial e checksums. Os scripts shell e JSON foram validados sintaticamente.

BLOCKED: comando dotnet build não executado porque o SDK .NET 10 não está instalado ou disponível no PATH.

BLOCKED: comando psql não executado porque o cliente PostgreSQL não está instalado ou disponível no PATH.

BLOCKED: comando pg_dump não executado porque o cliente PostgreSQL não está instalado ou disponível no PATH; por isso o artefato custom `.backup` não foi gerado.

BLOCKED: comando smoke MVC não executado porque o SDK .NET 10 e uma instância PostgreSQL não estão disponíveis no ambiente.

BASE LOCAL usada porque o remote `origin` e `origin/main` não estavam disponíveis.

BLOCKED: comando gh pr create não executado porque o repositório não possui remote `origin` configurado e o GitHub CLI não possui autenticação (`GH_TOKEN`/login) no ambiente.
