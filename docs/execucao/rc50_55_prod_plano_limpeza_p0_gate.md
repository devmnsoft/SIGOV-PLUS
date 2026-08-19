# RC50.55-PROD — plano de limpeza dos P0 do gate

Data: 2026-08-19. Baseline: `964ffd7` (RC50.54).

## Inventário e tratamento de projeções

A busca inicial encontrou **52 ocorrências executáveis**: 49 em C# (`Api`, `Infrastructure` e `Web`), uma na migration `024_assistencia_social_base.sql` e duas cópias nos scripts PostgreSQL consolidados. A view `vw_social_familias_risco` e todas as consultas estáticas passaram a declarar as colunas. Consultas sobre tabelas variáveis agora usam allowlists de tabela/projeção; as duas consultas orientadas pelo inspetor de schema montam uma lista de identificadores escapados a partir das colunas já validadas. O SQL continua parametrizado e os filtros de tenant/exclusão lógica foram preservados.

| Contexto | Quantidade inicial | Risco | Decisão/correção |
|---|---:|---|---|
| C# runtime | 49 | exposição LGPD, payload e I/O excessivos, contrato instável; uma consulta concatenava `id` | P0 corrigido com projeções explícitas/allowlist; localização passou a manter parâmetro |
| Migration 024 (view) | 1 | contrato da view muda com a tabela | P0 corrigido na fonte com 23 colunas explícitas |
| Consolidados PostgreSQL | 2 | propagação da view insegura | regenerados/sincronizados a partir da fonte e novo checksum |

A validação final case-insensitive retorna zero. Não há falso positivo em comentário.

## Validadores

Os validadores retornam código 0, mas mantêm diagnóstico conservador: 49 migrations em índices parciais, 126 avisos de garantia estática de colunas e 7 usos de `coalesce` potencialmente simplificáveis. Não há expressão comprovadamente não `IMMUTABLE`.

* **P0:** nenhum P0 estático comprovado; uma falha no apply limpo/parcial promoverá o item correspondente imediatamente.
* **P1:** 49 + 126 avisos históricos/condicionais até prova PostgreSQL limpa e parcial; mantidos visíveis, sem supressão.
* **P2:** 7 avisos conservadores de `coalesce` em índices válidos (expressões imutáveis); melhoria futura por coluna materializada, sem alterar chaves históricas nesta sprint.

## Decisão planejada

Build, banco e runtime precisam de `dotnet`/PostgreSQL. Neste host as ferramentas não existem, portanto o gate deve e efetivamente permanece bloqueado como `P0_ENVIRONMENTAL`; não será declarado verde sem evidência externa.
