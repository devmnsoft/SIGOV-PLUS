# RC50.49-C — SQLSTATE 42703 em `data_referencia`

## Causa raiz e impacto
A RC50.38 declarava `data_referencia` apenas em `CREATE TABLE IF NOT EXISTS`. Em bancos parcialmente migrados, as tabelas de Saúde já existiam e o PostgreSQL não incorporava a coluna; os índices `ix_saude_*_status_data` e `ux_saude_vacinacao_dose_dia` chegavam então a `ComputeIndexAttrs` com uma coluna ausente.

## Correção
A própria migration `20260816120000_rc50_38_saude_bloco7_core.sql` agora garante, antes de cada índice, `tenant_id`, `data_referencia timestamptz`, `status` e `is_deleted`. Os campos dos índices especiais de paciente, agenda e vacinação também são garantidos. A coluna permanece anulável e não houve backfill: inventar uma data para registros históricos alteraria sua semântica.

## Prevenção e validação
```bash
./scripts/check-migration-partial-index-columns.sh database/postgres/migrations
./scripts/check-migration-index-columns.sh database/postgres/migrations
python -m json.tool database/postgres/migrations/manifest.json
psql -h localhost -p 5432 -U postgres -d postgres -v ON_ERROR_STOP=1 -f database/postgres/script_completo_dev.sql
```

O validador novo analisa índices simples, relaciona tabela e colunas e falha quando a garantia não aparece antes do índice. O protocolo de execução passa a exigir essa verificação. O resultado real de PostgreSQL, build, Swagger e login deve ser registrado no relatório da execução, sem inferir sucesso quando o serviço estiver indisponível.
