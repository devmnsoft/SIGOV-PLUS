# Diagnóstico de migrations Pós-RC 16

## Inventário

As migrations PostgreSQL versionadas sob `database/postgres/migrations` estão organizadas por nome e devem ser aplicadas em ordem alfabética.

## Validação local

A execução com `psql -v ON_ERROR_STOP=1 -f` não pôde ser realizada neste container porque `psql` não está instalado. Não foi declarado sucesso de aplicação de migrations.

## Pontos de atenção mantidos para CI

- Confirmar referências a `sigov.outbox_evento` em vez de `sigov.outbox`.
- Confirmar idempotência de `CREATE TABLE IF NOT EXISTS`, índices e constraints em blocos `DO $$` quando necessário.
- Confirmar compatibilidade entre `tenant_id` `bigint` e `uuid` por tabela antes de criar FKs.
- Reexecutar seed demo duas vezes contra banco vazio e banco já migrado.
