# RC50.49-E — SQLSTATE 42703 em Frotas/Obras

## Causa raiz e impacto

A migration `20260816123000_rc50_38_frotas_obras_bloco7_core.sql` criava tabelas com `CREATE TABLE IF NOT EXISTS`, mas o bloco de compatibilidade legado garantia somente `status`, `ativo` e `is_deleted`. Em banco parcialmente migrado, a tabela já existente não recebia `data_referencia`; em seguida, os índices `ix_<tabela>_status_data` tentavam usar `(tenant_id, status, data_referencia)` e o PostgreSQL encerrava `ComputeIndexAttrs` com SQLSTATE `42703`.

O risco abrangia as 15 tabelas indexadas: `frota_veiculo`, `frota_motorista`, `frota_abastecimento`, `frota_manutencao`, `frota_viagem`, `frota_ocorrencia`, `frota_equipamento`, `obra`, `obra_etapa`, `obra_medicao`, `obra_fiscalizacao`, `obra_diario`, `obra_ocorrencia`, `obra_integracao_contrato` e `obra_evento`.

## Correção

Antes de cada índice, a migration agora garante `tenant_id bigint`, `status varchar(40)`, `data_referencia timestamptz`, `ativo boolean` e `is_deleted boolean`. Foi mantido `timestamptz`, tipo canônico já declarado pelo Bloco 7, para não criar divergência entre instalações novas e legadas. Nenhuma coluna tornou-se obrigatória retroativamente, nenhuma tabela/linha foi removida e nenhum índice foi descartado.

Um helper temporário faz backfill somente quando `created_at` existe, copiando esse timestamp apenas para linhas cuja `data_referencia` está nula. Ele não usa `current_date`; ao final, o helper é removido. As migrations de Saúde, Assistência Social e Saneamento também foram auditadas; seus blocos de compatibilidade garantem as colunas simples usadas pelos índices. Expressões de conversão remanescentes em Saúde existem em `UPDATE`/trigger, não em `CREATE INDEX`.

Os dashboards `/Frotas/Dashboard` e `/Obras/Dashboard` fazem preflight por `IDatabaseObjectInspector`: tabela ou coluna ausente produz indicadores zerados e estado **Estrutura pendente**, sem executar consulta incompatível. O ProjectStatus lista os quatro domínios RC50.38 e apresenta diagnóstico de SQLSTATE `42703`/`42P17` sem expor connection string.

## Validação reproduzível

```bash
python -m json.tool database/postgres/migrations/manifest.json
./scripts/check-migration-partial-index-columns.sh database/postgres/migrations
./scripts/check-migration-index-columns.sh database/postgres/migrations
./scripts/check-migration-immutable-index-expressions.sh database/postgres/migrations
psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql
dotnet build sigov.runtime.slnf --configuration Release --nologo -warnaserror
bash scripts/check-api-route-conflicts.sh
```

No catálogo real, confirmar `data_referencia`, os índices `ix_frota_*_status_data`/`ix_obra_*_status_data` e as versões `20260816120000` a `20260816123000` em `sigov.schema_migrations`.

## Regra preventiva

Toda tabela criada com `IF NOT EXISTS` deve possuir um bloco idempotente `ALTER TABLE ... ADD COLUMN IF NOT EXISTS` para **cada** coluna consumida depois por índice, constraint, view ou query. Funções/casts de data ficam em backfill ou trigger; índices usam somente colunas materializadas.

## Resultado do ambiente desta execução

Os validadores estáticos e o build devem ser registrados no fechamento da execução. Aplicação PostgreSQL, Swagger e login só podem ser declarados validados quando `psql`, runtime e serviços locais estiverem disponíveis; ausência dessas dependências é pendência ambiental, não evidência de sucesso.
