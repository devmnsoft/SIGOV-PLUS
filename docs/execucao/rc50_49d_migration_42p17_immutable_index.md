# RC50.49-D — SQLSTATE 42P17 em expressão de índice

## Causa raiz

O índice único `ux_saude_vacinacao_dose_dia`, da tabela `sigov.saude_vacinacao`, usava `(data_referencia::date)`. Como `data_referencia` é `timestamptz`, o resultado desse cast depende do fuso horário da sessão. O PostgreSQL o classifica como não `IMMUTABLE` e rejeita a expressão em `CREATE INDEX` com SQLSTATE `42P17` (`ComputeIndexAttrs`).

## Correção segura

A migration `20260816120000` agora cria e garante a coluna materializada `data_referencia_dia date`. Registros existentes com dia ausente recebem o dia de `data_referencia` normalizado explicitamente em UTC. Um trigger mantém a coluna sincronizada em inclusões e alterações futuras, sem declarar nenhuma função como `IMMUTABLE`. O índice preserva a unicidade de dose por dia usando somente colunas reais: `tenant_id`, `paciente_id`, `nome`, `tipo` e `data_referencia_dia`.

O backfill altera apenas linhas em que `data_referencia_dia is null` e `data_referencia is not null`; não remove tabelas, índices, linhas ou dados informados.

## Validação

```bash
./scripts/check-migration-partial-index-columns.sh database/postgres/migrations
./scripts/check-migration-index-columns.sh database/postgres/migrations
./scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations
python -m json.tool database/postgres/migrations/manifest.json
psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql
```

Após aplicar no PostgreSQL, consultar `sigov.schema_migrations`, `information_schema.columns` e `pg_indexes` para confirmar a versão, a coluna materializada e a definição do índice. O resultado de banco, Swagger e login só deve ser declarado quando o respectivo serviço estiver disponível.

## Regra preventiva

Não colocar casts ou funções `STABLE`/`VOLATILE` em expressões de índice. Datas derivadas, competências e textos normalizados devem ser persistidos em coluna explícita e o índice deve referenciar apenas essa coluna simples.
