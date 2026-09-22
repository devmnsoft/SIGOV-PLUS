# RC51.03 — recebimento parcial de compras empresariais

Data: 2026-09-16. Estado: **IMPLEMENTADO SEM HOMOLOGAÇÃO RUNTIME**.

## Diagnóstico e fontes

Foram usados o contrato vigente em `docs/execucao/CODEX_EXECUTION_CONTRACT.md` (Contrato técnico e Maturidade), o estado real descrito em `README.md` (Banco de dados, Build e testes e Estado funcional), o fluxo histórico em `docs/estoque-compras.md` e a separação multiesfera de `docs/COMPRAS-LICITACOES-CONTRATOS-MULTIESFERA.md`. Os dois últimos são referência de fronteira; não foi inferida política comercial de tolerância, reposição, pagamento ou contratação pública.

Preflight: branch `work`, HEAD inicial `f0c26e925b1854e27a4b9987f86155f1f9776e25`, árvore limpa, sem remoto e sem upstream. SDK normativo `10.0.100`; `dotnet`, `psql`, PostgreSQL, Docker e navegador não estão instalados.

| Funcionalidade | Implementação encontrada | Lacuna | Mudança | Evidência de aceite |
|---|---|---|---|---|
| Regressões anteriores | guarda nula SaaS explícita; aliases de Governança e `PagedResult` já corrigidos | gate runtime pendente | preservadas, sem reimplementação | inspeção estática e testes existentes |
| Central de recebimentos | rotas eram `Workspace` sem dados | sem filtros, total ou paginação real | consulta Dapper tenant-scoped, filtros, totais, ordenação e tela responsiva | código, rota protegida e teste existente ampliado |
| Recebimento parcial | cabeçalho legado sem itens persistidos | sem saldo por item, idempotência operacional ou estoque | migration forward-only, itens físicos/aceitos/conferência/rejeitados, bloqueio de excesso, lock do pedido e chave idempotente | invariantes SQL e releitura antes do commit |
| Estoque | `estoque_saldo` e `estoque_movimento` canônicos | compra não os integrava | entrada atômica apenas da quantidade aceita; material em conferência não fica disponível | mesma transação Dapper e origem persistida |
| Divergência/reversão | devolução legada somente em cabeçalho | fluxo operacional incompleto | estrutura preserva rejeição e eventos; nenhuma reabertura automática de pedido foi inventada | permanece backlog explícito |
| Template/formulário | padrão Compras existente | telas eram placeholder | cabeçalho, totais, filtros, tabela contida, vazio, ajuda, antiforgery, validação e bloqueio de duplo envio | Razor/CSS e rota protegida |

## Matriz de transições implementada

| Inicial | Ação | Pré-condições e responsável | Efeito | Final |
|---|---|---|---|---|
| `CONFIRMADO` / `PARCIALMENTE_RECEBIDO` | confirmar recebimento | usuário autorizado, tenant, versão, fornecedor/pedido, destino ativo, itens do pedido e saldo sob lock | registra físico e auditoria; item sem inspeção entra no estoque | pedido `PARCIALMENTE_RECEBIDO` ou `RECEBIDO`; recebimento `CONCLUIDO` |
| mesmos estados | confirmar item que exige inspeção | mesmas condições | registra físico em conferência, sem saldo disponível | recebimento `EM_CONFERENCIA` |
| qualquer outro estado | receber | nenhuma | rollback com explicação | inalterado |
| chave já persistida | repetir requisição | mesmo tenant e chave | retorna operação persistida, sem nova movimentação | inalterado |

Não há tolerância de excesso: quantidade física não pode exceder pedido menos cancelado menos físico já registrado. Não há conversão automática de unidade. A migration não cria saldo paralelo e usa PK `bigint identity` nas novas entidades. Recebimento não cria fatura, pagamento, liquidação, reserva ou consumo industrial.

## Arquivos e migration

A migration `20260916120000_compras_recebimento_parcial.sql` foi adicionada ao manifesto e aos seis scripts canônicos. Contratos, application service, repositório Dapper, DI, controller MVC, views, CSS e testes existentes foram atualizados.

## Validação e bloqueios

Passaram: JSON do manifesto, `git diff --check`, varredura precisa de marcadores de conflito e inspeções estáticas. **BLOCKED separadamente:** restore/build/test/Razor/OpenAPI; PostgreSQL 16 limpo/upgrade/reexecução e cenários concorrentes; navegador, acessibilidade e capturas em 360–1920 px. As ferramentas não existem neste container, portanto a jornada não é declarada homologada.

## Riscos e próximo backlog exato

O cabeçalho legado do pedido e do recebimento mantém UUID por compatibilidade; os novos itens do pedido, itens do recebimento e eventos usam bigint identity. A etapa de decisão da conferência, devolução física e estorno com bloqueio por movimentos posteriores ainda não foi implementada. Próximo item exato: disponibilizar SDK 10.0.100 e PostgreSQL 16, executar Gate A e os cenários concorrentes; em seguida implementar `EM_CONFERENCIA → CONCLUIDO/COM_DIVERGENCIA` e devolução/estorno sem reabrir automaticamente o pedido.

## Correção RC51.03A — retry concorrente (2026-09-22)

A auditoria da ordem dos comandos confirmou uma janela no contrato de idempotência: a consulta inicial da chave ocorria antes de `FOR UPDATE`. Duas transações podiam ler “ausente”; a segunda aguardava o lock e, depois do commit da primeira, era recusada pela versão do pedido já incrementada. Isso não duplicava o movimento, mas convertia um retry válido em erro e contrariava a transição documentada para chave já persistida.

A consulta de idempotência foi centralizada e repetida imediatamente após a aquisição do lock, ainda na mesma transação e antes da validação de estado/versão. Em `READ COMMITTED`, o segundo comando passa a enxergar o recebimento confirmado e retorna seu identificador/status sem inserir item, evento, saldo ou movimento novamente. Chaves diferentes continuam submetidas ao lock, à versão e ao saldo pendente; não foi introduzida tolerância nem regra financeira.

Uma regressão foi adicionada à classe de teste existente para proteger a ordem consulta → lock → nova consulta. Os gates estáticos passaram. A prova concorrente em PostgreSQL 16 e o teste .NET permaneceram **BLOCKED** porque o ambiente não contém `dotnet`, `psql`, connection string nem aplicações iniciadas; portanto, esta correção permanece sem homologação runtime. A interface não mudou e não houve captura de tela aplicável.
