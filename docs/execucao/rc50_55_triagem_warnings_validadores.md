# RC50.55 — triagem dos warnings dos validadores

Data: 2026-08-19.

| Validador | Exit code | Resultado | Classe |
|---|---:|---|---|
| `check-migration-partial-index-columns.sh` | 0 | 49 avisos conservadores | P1 até apply limpo/parcial |
| `check-migration-index-columns.sh` | 0 | 1215 índices verificados; 126 avisos | P1 até apply limpo/parcial |
| `check-migration-immutable-index-expressions.sh` | 0 | 114 migrations; 7 avisos de `coalesce` | P2, expressão imutável |

Os P1 são limitações do parser diante de DDL histórico, compatibilidade e criação condicional; não foram escondidos. A saída integral está nos três artifacts RC50.55. Os P2 não contêm `now()`, `current_date`, `date_trunc`, cast de timestamp para data nem `unaccent` no índice. Nenhum warning foi rebaixado contra uma falha real: como o host não possui PostgreSQL, a comprovação de apply segue obrigatória e a decisão permanece bloqueada.
