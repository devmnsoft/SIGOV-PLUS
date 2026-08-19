# RC50.54 — triagem dos warnings dos validadores

## Inventário

A RC50.53 registrou 49 avisos conservadores de predicados de índices parciais, 126 de garantia de colunas e 7 de `COALESCE` em expressões. Os validadores retornaram zero, portanto esses itens não foram diagnosticados como violações comprovadas; a saída completa deve ser preservada pelo job `static-validation`.

| Prioridade | Classificação | Tratamento RC50.54 |
|---|---|---|
| P0 | coluna não existente no ponto do `CREATE INDEX`, expressão volátil ou falha no apply limpo/parcial | bloqueio automático; nenhum P0 estático foi encontrado na baseline; qualquer falha real deve corrigir a migration e checksum antes do merge |
| P1 | análise conservadora de DDL condicional/dinâmico ou migration histórica mitigada por preflight | manter visível, confirmar nos dois applies PostgreSQL e refatorar primeiro migrations recentes/áreas Segurança, LGPD, Auditoria e Saúde |
| P2 | casamento em comentário ou incapacidade documentada do parser sem risco no PostgreSQL | documentar junto da saída; nunca usar `NoWarn` nem desativar o validador |

## Decisão

Não há P0 conhecido corrigível apenas pela inspeção atual. A convergência dos jobs `database-clean-apply` e `database-partial-apply` é condição necessária para reclassificar os avisos históricos como P1/P2. Falha SQL promove imediatamente o item a P0.

## P0 estático adicional encontrado

A busca RC50.54 encontrou usos históricos de `SELECT *` em migrations, Infrastructure, API e Web. Embora não sejam warnings dos três parsers de índice, violam a política deste gate e são **P0 aberto**: o job estático falhará até que cada consulta tenha projeção explícita. A correção ampla não foi mascarada nem realizada parcialmente nesta entrega de automação, para não alterar dezenas de contratos de serialização sem banco/build/runtime disponíveis.
