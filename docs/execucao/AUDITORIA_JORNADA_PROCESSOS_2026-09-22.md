# Auditoria e incremento da jornada de processos — 2026-09-22

## 1. Baseline e decisão de escopo

- Branch auditado: `work`; commit inicial: `6fdfc58d2c5d5e953a2b64141d556ceb9fe7de72`; árvore inicialmente limpa.
- Stack confirmada: SDK .NET `10.0.100`, C# 14, camadas Domain/Application/Infrastructure/Api/Web, PostgreSQL e Dapper.
- Migrations canônicas: `database/postgres/migrations/manifest.json`, runner Dapper e scripts consolidados.
- O ambiente não contém `dotnet`, `psql`, Docker, PowerShell ou `ConnectionStrings__DefaultConnection`. A tentativa de obter o SDK oficial retornou HTTP 403. Portanto build, integração PostgreSQL, autenticação real e navegador autenticado ficaram **BLOQUEADOS**.
- Em respeito ao Gate A, não foram criados schema, catálogo de permissão, prazo legal, seed ou fluxo paralelo. O incremento ficou limitado a correções independentes e comprováveis por inspeção na jornada canônica `Processos`.

## 2. Rastreio real

| Capacidade | Classificação neste corte | Evidência e lacuna |
|---|---|---|
| Menu/rota Web | PARCIAL | `ProcessosController`, views `Views/Processos` e JavaScript do módulo existem; navegação autenticada não executada. |
| API | PARCIAL | `ProcessosDigitaisController` expõe consulta, abertura, edição, movimentação, parecer, conclusão e cancelamento; runtime não executado. |
| Aplicação/autorização | PARCIAL | `ProcessoDigitalService` usa catálogo persistido via `IPermissionService`; não há prova com dois perfis/tenants. |
| Persistência | PARCIAL | repositórios Dapper e tabelas publicadas existem; integração PostgreSQL bloqueada. |
| Numeração | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | `ProcessoSequencialRepository` usa advisory lock e controle sequencial por tenant/entidade/exercício/ano; concorrência real não executada. |
| Distribuição/encaminhamento | PARCIAL | movimento e destino existem. O insert de histórico e update do processo não eram atômicos e não detectavam concorrência; corrigidos neste incremento. Recebimento formal não foi identificado no modelo canônico. |
| Parecer | PARCIAL | criação e consulta existem; versionamento/finalização não foram identificados. |
| Conclusão/cancelamento | PARCIAL | comandos e permissões existem. Agora exigem justificativa, registram evento e alteração na mesma instrução e recusam estado desatualizado; validação de pendências obrigatórias depende de regra persistida ainda não identificada. |
| Prazos | PARCIAL | `prazo_resposta_at` é persistido/exibido, mas calendário/regra aplicada/suspensão não estão modelados. A UI explicita “Prazo não configurado”. |
| Anexos/GED | NÃO VERIFICADA EM RUNTIME | estruturas separadas existem; download autorizado ponta a ponta não foi executado. |
| Central/notificações | PARCIAL | infraestrutura transversal existe, mas a jornada canônica auditada não cria/deduplica pendência em cada transição. Não foi simulada integração. |
| Histórico | PARCIAL | movimentações e pareceres são consultados. Conclusão/cancelamento passaram a integrar a timeline; recebimento, prazo e reabertura canônicos continuam ausentes. |
| Relatórios/exportação | AUSENTE APÓS BUSCA DIRIGIDA | não foi identificado relatório canônico desta jornada nas views/API `Processos`. |

## 3. Regras confirmadas e correções

### Fontes confirmadas no código/schema

- Estados publicados: `ABERTO`, `EM_TRAMITACAO`, `AGUARDANDO_DOCUMENTO`, `AGUARDANDO_ASSINATURA`, `SUSPENSO`, `ENCERRADO`, `CANCELADO`.
- Estados finais não admitem movimentação.
- Permissões existentes e reutilizadas: visualizar, criar, editar, excluir, movimentar, parecer, encerrar e cancelar.
- Isolamento primário por `tenant_id`; listagem agora também aplica `entidade_id` e `exercicio_id` quando presentes no contexto.

### Matriz de transição deste incremento

| Estado atual | Ação | Perfil autorizado | Pré-condições | Estado resultante | Efeitos atômicos | Bloqueio |
|---|---|---|---|---|---|---|
| estado não final | encaminhar | `processos.processo.movimentar` | despacho; unidade ou responsável; estado esperado opcional | estado operacional permitido, padrão `EM_TRAMITACAO` | atualiza destino/status e insere movimentação | finalizado, estado concorrente ou destino ausente |
| estado não final | concluir | `processos.processo.encerrar` | usuário autenticado; justificativa; estado esperado opcional | `ENCERRADO` | encerra e insere evento na timeline | finalizado ou estado concorrente |
| estado não final | cancelar | `processos.processo.cancelar` | usuário autenticado; justificativa; estado esperado opcional | `CANCELADO` | cancela e insere evento na timeline | finalizado ou estado concorrente |
| `ENCERRADO`/`CANCELADO` | editar/movimentar/concluir/cancelar | qualquer | — | inalterado | nenhum | estado final |

O contrato aceita `statusEsperado`; se o registro mudar entre leitura e comando, a API devolve HTTP 409 e orienta recarga. A UI envia o estado lido, desabilita reenvio do comando durante a requisição e só anuncia sucesso após resposta persistida.

A listagem tinha parâmetros de contexto e período que não eram usados pelo SQL. Ela agora usa tenant, entidade, exercício, início/fim e ordenação determinística tanto no contador quanto nos itens.

## 4. Interface e ajuda revisadas

Cobertura efetivamente alterada:

- `/Processos`: saída textual dinâmica codificada contra injeção de HTML, prazo ausente explícito e encaminhamento com estado esperado.
- `/Processos/Detalhe/{id}`: ajuda contextual específica com finalidade, funcionamento, pré-requisitos, regras e próximo passo; situação/prazo; ação principal; conclusão/cancelamento com justificativa; timeline com transições.
- Modal de encaminhamento: semântica acessível, explicação do efeito, destino/responsável e prevenção de envio duplicado.

Limitação explícita: os campos de destino ainda recebem códigos técnicos porque não foi encontrado catálogo Web nominal autorizado pronto para reutilização. Não foi criado fallback hardcoded. Esta tela permanece **PARCIAL** até existir/ser integrado o seletor persistido.

## 5. Banco e compatibilidade

Nenhuma alteração de schema foi necessária; portanto nenhuma migration ou consolidado foi alterado. As correções usam colunas/tabelas já publicadas. O SQL novo é uma única instrução com CTEs modificadores e lock de linha, preservando atomicidade local. Clean install, upgrade, reaplicação e teste concorrente permanecem bloqueados sem PostgreSQL 16.

## 6. Evidências

### PASSOU

- `git diff --check`.
- `node --check src/Sigov.Web/wwwroot/js/modules/processos.digital.js`.
- `bash scripts/check-migration-catalog.sh`: 195 SQLs, 185 no manifesto, 181 no baseline, 10 órfãs governadas; validação estática PASS.

### FALHOU

- Nenhuma verificação executada encontrou falha do incremento.

### BLOQUEADO

- `dotnet restore`, `dotnet build` e `dotnet test`: executável `dotnet` ausente; download oficial bloqueado com HTTP 403.
- Integração PostgreSQL, instalação limpa, upgrade, reaplicação, concorrência e dois tenants: `psql`, servidor e connection string ausentes.
- Inicialização Web/API, autenticação, screenshot e validação responsiva: runtime e navegador autenticado indisponíveis.

### NÃO EXECUTADO

- Recebimento formal, reabertura, calendário útil, anexos, notificações deduplicadas e relatórios não foram declarados concluídos; dependem de regras/catálogos e validação integrada não disponíveis neste corte.

## 7. Pendências e próximo incremento

1. Prover ambiente seguro com SDK 10.0.100, PostgreSQL 16 e dois tenants/perfis; executar restore/build/test e cenários concorrentes.
2. Confirmar no banco a fonte de unidades e usuários ativos e integrar seletores nominais, validando vínculo no mesmo comando transacional.
3. Definir se recebimento é etapa formal e quais permissões a regem; não há regra canônica suficiente para inventá-la.
4. Parametrizar pendências impeditivas de conclusão e regras de prazo/calendário; preservar a versão da regra aplicada.
5. Integrar as transições à central/notificação/outbox existente com chave idempotente, sem duplicar infraestrutura.
6. Auditar autorização de detalhe/parecer/anexo por entidade e sigilo com dois tenants antes de promover a jornada.
