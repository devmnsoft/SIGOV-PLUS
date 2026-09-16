# RC51.04 — transferências e fundação de inventário de estoque

Data: 2026-09-16. Estado: **PARCIAL, SEM HOMOLOGAÇÃO RUNTIME**.

## Fontes efetivamente consultadas

Foram aplicados o contrato técnico e os critérios de maturidade de `docs/execucao/CODEX_EXECUTION_CONTRACT.md`, a jornada canônica do `docs/FUNC02-ALMOXARIFADO-ESTOQUE-REQUISICOES.md`, o estado operacional do `README.md` e a entrega anterior `docs/entregas/RC51-03-RECEBIMENTO-COMPRAS.md`. O inventário patrimonial de FUNC01 foi apenas delimitado: patrimônio e estoque de consumo continuam domínios distintos.

## Preflight e regressões

Branch inicial `work`, HEAD `c857f9b71d8dc38ce7cd527f2e0df9991c0cc51b`, árvore limpa, sem remoto e sem upstream configurados. `global.json` exige SDK 10.0.100, porém `dotnet`, `psql`, PostgreSQL, Docker, PowerShell e navegador não estão disponíveis. As correções de nullable SaaS, aliases de Qualidade, paginação e suspensão já estavam integradas no HEAD; sem runtime não foram declaradas homologadas.

| Jornada | Código existente | Lacuna confirmada | Alteração desta RC | Aceite ainda necessário |
|---|---|---|---|---|
| saldo e movimento | `almoxarifado_estoque` e `almoxarifado_movimentacao` | transferência existente sem serviço operacional | saída/entrada usam a mesma fonte canônica e uma transação por ação | PostgreSQL 16, concorrência e rollback |
| transferência | tabelas RC50.89 e rota que redirecionava ao estoque | sem rascunho, expedição ou recebimento parcial | contratos, Dapper, API, MVC e páginas reais | jornadas integral, parcial, excesso, duplicidade e dois tenants |
| divergência | quantidade conferida única | recusas não eram preservadas | recusa e motivo persistidos; não viram perda ou entrada | devolução/reversão vinculada |
| rastreabilidade | movimento tinha documento de origem | faltava vínculo por item/remessa | IDs de movimentos de saída/entrada e eventos idempotentes persistidos | consulta transversal compras–indústria |
| inventário físico de estoque | somente inventário patrimonial e tabela empresarial genérica | inexiste campanha canônica de consumo com snapshot, bloqueio, versões e segregação | sem implementação paralela; sem promover patrimônio a estoque | próximo item obrigatório descrito abaixo |

## Semântica canônica do estoque

- **Físico** é `almoxarifado_estoque.quantidade`; somente movimentos confirmados o alteram.
- **Reservado** é a separação de entrega/requisição existente; ainda não há coluna de reserva agregada. A disponibilidade não pode ser calculada subtraindo essa reserva duas vezes.
- **Bloqueado** ainda não possui representação canônica. Deve ser criado pelo inventário físico e validado em todos os caminhos de movimento antes de liberar a estratégia A.
- **Disponível** é físico menos compromissos persistidos aplicáveis; a implementação atual de transferência revalida o físico sob lock, mas o agregador único de reservas permanece uma lacuna.
- **Em trânsito** é `quantidade expedida - quantidade recebida`; recusas permanecem pendentes até tratamento autorizado e não viram perda.
- **Sob conferência** pertence ao recebimento de compras da RC51.03 e não integra o disponível até decisão de inspeção.

Lote participa dos movimentos e itens de transferência, mas o saldo legado ainda tem chave `(tenant, entidade, almoxarifado, material)`, não lote/localização interna. Alterar essa chave sem migração de saldos seria destrutivo; por isso inventário por lote não foi simulado. Material permanente recebido gera pendência patrimonial, nunca tombamento automático.

## Regras entregues

O documento nasce em `RASCUNHO`, sem movimento. Origem e destino devem ser locais ativos da mesma entidade do contexto, sempre no mesmo tenant; combinação entre entidades falha explicitamente porque não existe regra de domínio aprovada. Expedição bloqueia documento e saldos, revalida disponibilidade e grava saída e trânsito atomicamente. Recebimentos podem ser parciais, são idempotentes, não excedem o pendente e geram entrada somente da quantidade aceita. Recusa exige divergência e mantém histórico; cancelamento pós-expedição não foi exposto.

## Formulários verificados estaticamente

| Tela | Binding e validação | Autorização | Serviço/SQL/transação | Nova leitura |
|---|---|---|---|---|
| nova transferência | seletores nominais, quantidade decimal, justificativa, responsável, esfera, antiforgery e idempotência | `transferencia.criar` | insert de cabeçalho/itens/evento | detalhe tenant-scoped |
| detalhe/expedição | POST curto, antiforgery e duplo envio bloqueável | `transferencia.expedir` | locks de documento/saldos, movimentos e evento | redirect para detalhe |
| recebimento parcial | campos por item, limites e motivo de recusa | `transferencia.receber` | lock, entrada, recebimento, acumulados e evento | redirect para detalhe |
| lista | filtro de estado e paginação | `transferencia.visualizar` | count e ordenação estável | página tenant-scoped |

## Riscos e próximo backlog exato

Esta RC não conclui inventário físico nem a consulta transversal de rastreabilidade, portanto o objetivo integrado permanece parcial. Próximo item exato: criar RC própria para inventário de consumo com **estratégia A**, incluindo escopo persistido, snapshot imutável, bloqueio aplicado em entrada, saída, entrega, transferência e consumo industrial, versões de contagem/recontagem, segregação de aprovação, ajuste por movimento e testes concorrentes em PostgreSQL 16. Depois, implementar devolução/reversão de transferência e a consulta transversal baseada exclusivamente nos vínculos persistidos.
