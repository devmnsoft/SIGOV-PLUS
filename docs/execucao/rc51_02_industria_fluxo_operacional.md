# RC51.02 — fluxo industrial operacional

Corte: 2026-09-15. Estado: **implementado sem validação runtime**; não homologado porque o checkout não dispõe de .NET, PostgreSQL, PowerShell nem navegador.

## Retomada e dependências

Branch `work`, HEAD inicial `0846c3df2d93198fbd9c8321f405d1d27bb844e2`, sem remoto ou upstream e árvore inicialmente limpa. O contrato normativo é .NET SDK 10.0.100/C# 14, MVC/Razor, API REST, PostgreSQL 16+ e Dapper.

| Dependência | Classificação | Evidência e limite |
|---|---|---|
| Ordens e estados | parcial | OP, histórico e permissões persistidos; transições antigas ainda precisam de prova concorrente no PostgreSQL |
| Ficha técnica/versionamento | parcial | a OP referencia a ficha e seus itens; falta fotografia imutável formal da composição liberada |
| Reserva/consumo | parcial | saldo comercial canônico usa lock/transação; reserva ainda não é individualizada por OP |
| Apontamento | implementada sem validação | confirmação agora exige idempotência, bloqueia a OP e limita o acumulado |
| Lotes/estoque | parcial | lote acabado e vínculos persistidos; saldo canônico não discrimina lote/almoxarifado |
| Qualidade/não conformidade | parcial | inspeção canônica preservada e quantidades distintas adicionadas; liberação do disponível ainda pendente |
| Custeio | parcial | custo cadastral ausente falha; cálculo histórico existente não foi promovido |
| SaaS/autorização | implementada sem validação | módulo e avaliador canônicos permanecem nas rotas; suspensão requer prova runtime |
| Auditoria | implementada sem validação | evento e correlation id permanecem na mesma transação do apontamento |
| Interface | parcial | template responsivo existente; jornada de edição dedicada e ensaio visual permanecem bloqueados |

## Regras e transições preservadas

- `PLANEJADA → LIBERADA → EM_PRODUCAO ↔ PAUSADA → CONCLUIDA`; cancelamento é terminal. Liberação requer perfil com `industria.ordens.liberar` e ficha quando o produto assim exige.
- Apontamento confirmado exige `industria.apontamentos.criar`, OP liberada/em produção/pausada, quantidade positiva para `PRODUCAO`, chave idempotente e acumulado dentro do planejado. A mesma chave e conteúdo retorna o registro; conteúdo diferente resulta em conflito. O lock da OP serializa confirmações concorrentes.
- `quantidade_planejada`, `quantidade_produzida`, `quantidade_aprovada`, `quantidade_rejeitada` e o saldo pendente de inspeção são conceitos separados. Entrada física não soma novamente a produção apontada.
- Consumo continua explícito: nenhuma fórmula proporcional foi inventada. Material reservado e consumido permanecem conceitos distintos.
- Conclusão produtiva não cria pagamento nem equivale a encerramento financeiro. Inspeção obrigatória pendente bloqueia conclusão.

## Riscos e próximo item

A nova cadeia de custódia permite vincular consumo, entrada e inspeção ao apontamento, mas as rotas legadas ainda não exigem esses vínculos. O próximo item exato é mover **consumo + entrada acabada + criação da inspeção** para uma única operação transacional confirmada por apontamento, individualizando reserva e lote no estoque canônico, seguida pelos cenários PostgreSQL 16 de dois tenants, retry, concorrência e rollback intermediário. GED permanece por último.
