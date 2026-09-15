# RC51.02 — distribuição interna de materiais

Data: 2026-09-15. Estado: **implementada sem validação runtime / BLOCKED**.

## Estado inicial e classificação

Branch `work`, HEAD inicial `0ab62eb5edc323271b774d825109d3d89b47497b`, árvore limpa, sem remoto e sem upstream. SDK normativo `10.0.100`, C# 14, ASP.NET Core MVC/Razor/API, PostgreSQL 16+ e Dapper. Somente Git, Node 20, npm 11 e Python estavam disponíveis; `dotnet`, `psql`, PostgreSQL, Docker, PowerShell e navegador estavam ausentes.

| Jornada | Estado inicial | Resultado desta fatia |
|---|---|---|
| Catálogo, locais, saldo e movimento | parcial | preservado como estoque canônico |
| Solicitação interna | parcial | fluxo existente rascunho → envio → autorização preservado; detalhe ganhou pendências e entregas |
| Autorização | parcial | política persistida existente preservada; nenhuma autoaprovação |
| Separação/reserva | ausente | persistida por entrega e item, com disponibilidade descontando outras separações |
| Expedição | atendimento monolítico parcial | saída, saldo, movimento e quantidade atendida passam pela mesma transação |
| Recebimento e divergência | ausente | conferência parcial/idempotente e divergência sem ajuste automático |
| Transferência, devolução e estorno | parcial em outros modelos / ausente nesta jornada | não foram tratados como distribuição nem implementados por falta de política canônica confirmada |
| Relatórios completos | parcial | consultas existentes preservadas; novos relatórios continuam pendentes |
| Formulários | implementados sem validação | Razor real, antiforgery, erros operacionais e impressão; browser indisponível |

## Contrato operacional aplicado

- **Solicitação:** solicitante autenticado e contexto vêm da sessão; itens positivos e materiais/locais do mesmo tenant e entidade são validados no banco. Rascunho não movimenta nem reserva.
- **Autorização:** `ENVIADA → APROVADA` exige a permissão canônica. A separação recusa qualquer outro estado.
- **Separação:** cria `SEPARADA`; a reserva é a soma persistida de itens em entregas separadas. Disponibilidade é saldo físico menos reservas concorrentes. A requisição e seus itens são bloqueados durante a decisão; chave idempotente evita duplicação.
- **Expedição:** somente `SEPARADA`; bloqueia entrega e saldo, baixa fisicamente a origem, cria movimento ligado à requisição e incrementa atendimento. Repetição após `EXPEDIDA` não duplica saída. Se restar quantidade, a solicitação continua `APROVADA`; caso contrário fica `ATENDIDA`. Não foi criado estoque em trânsito.
- **Recebimento:** somente entrega expedida ou parcialmente conferida; recebido + recusado nunca excede expedido. Confirmações são serializadas e idempotentes. Recebimento de unidade consumidora não cria saldo. Divergência/recusa é registrada e não gera ajuste, descarte ou devolução automática.
- **Devolução e estorno:** permanecem pendentes de política e permissão específicas; movimento não é apagado e divergência não é devolvida silenciosamente ao disponível.

## Persistência e concorrência

A migration forward-only cria entrega, itens e eventos com PK `bigint identity`, isolamento por tenant/entidade, constraints quantitativas, índices de fila e idempotência. A saída reutiliza `almoxarifado_estoque` e `almoxarifado_movimentacao`; não existe segundo saldo. Todas as escritas operacionais usam SQL parametrizado e transação Dapper.

## Validação e bloqueios

Passaram JSON do manifesto, verificação estática de whitespace/diff e busca por conflitos. Restore locked, build Release, Razor, testes, Swagger, rotas Web, PostgreSQL vazio/upgrade/reexecução, dois tenants, concorrência real, screenshots e acessibilidade nas larguras solicitadas ficaram **BLOCKED** pela ausência de SDK, banco e navegador. Não há declaração de homologação.

## Pendências e próximo backlog

A unidade solicitante ainda não possui catálogo seletor autorizado no formulário legado; relatórios paginados/exportações de pendências e divergências, cancelamento de separação, devolução e estorno requerem política persistida. Próximo item exato: implementar cancelamento concorrente de entrega `SEPARADA` que libere exclusivamente sua reserva, seguido de política persistida de devolução/estorno e prova PostgreSQL 16.
