# Fechamento funcional — conferência de recebimentos de compras

Data: 2026-09-22. Baseline: branch `work`, commit `03641ed6ec286e4cf796eccbcf8211ca8a97c5bf`, árvore inicialmente limpa.

## Inventário dirigido e prioridade

A auditoria foi dirigida às entregas recentes e às integrações solicitadas; não representa auditoria integral das 1.013 views já inventariadas.

| Capacidade | Classificação inicial | Evidência → impacto → dependência → aceite |
|---|---|---|
| Administração SaaS | implementada com evidência estática; runtime não verificado | serviço protege cadastro/revogação e teste existente foi ajustado → risco concorrente tratado → depende de PostgreSQL/runtime para homologação → build e matriz A/B ainda requeridos |
| Educação/matrícula | implementada com evidência estática; runtime não verificado | lock de oferta e versão na conversão → evita consumo concorrente → PostgreSQL necessário → cenários concorrentes permanecem bloqueados neste ambiente |
| Processos/tramitação | implementada com evidência estática; runtime não verificado | transições, versão e histórico no repositório → reduz perda de atualização → PostgreSQL necessário → execução autenticada permanece bloqueada |
| Compras/registro de recebimento | parcial | recebimento gravava físico e estoque, mas `/Recebimentos/{id}` era placeholder → usuário não relia nem concluía inspeção → tabelas e permissão existentes → detalhe, conferência e histórico navegáveis |
| Compras/conferência | ausente após busca | `EM_CONFERENCIA` era criado sem comando de saída → itens ficavam indefinidamente indisponíveis → serviço/repositório/tabelas existentes → decisão atômica aceita/rejeita e atualiza estoque |
| Central de pendências | parcial | central canônica existia em `pendencia_operacional`, sem projeção do recebimento → trabalho sem fila transversal → vínculo `enterprise_tenant_mapping` → abre ao receber e resolve ao concluir |
| Relatório de recebimentos | quebrada para a jornada | rota abria workspace sem dados → totais não eram auditáveis → consulta canônica existente → relatório abre os mesmos registros e exporta CSV protegido |
| Ajuda contextual | parcial | formulário tinha somente aviso curto → regras e próximo passo não estavam explícitos → componentes visuais existentes → cinco seções específicas em lista, criação e detalhe |

A jornada escolhida foi **recebimento sujeito a inspeção**, categoria **(b) operação iniciada que não podia ser concluída pela interface**. A prioridade decorre do risco de material físico permanecer em conferência sem ação possível, divergência não registrada e estoque operacionalmente inconsistente. Não foram reimplementados autenticação, permissão, estoque, histórico nem central.

## Contrato funcional

O operador com `recebimentos.registrar` inicia a partir de pedido confirmado, informando destino, documento, data e quantidades. Itens sem inspeção entram no estoque na confirmação; itens inspecionáveis ficam retidos. Um usuário com `recebimentos.inspecionar` classifica integralmente cada quantidade entre aceita e rejeitada. Aceitos entram no estoque; rejeitados não entram, exigem justificativa e tornam o recebimento `COM_DIVERGENCIA`. A correção por estorno/devolução não foi inventada: permanece dependente da regra do domínio. Resultado, itens e histórico são consultados no detalhe, central, relatório e CSV.

| Estado atual | Ação | Permissão | Pré-condições | Estado resultante | Efeitos | Bloqueio |
|---|---|---|---|---|---|---|
| `CONFIRMADO`/`PARCIALMENTE_RECEBIDO` | confirmar recebimento | `recebimentos.registrar` | versão, tenant, pedido, destino e saldo válidos | `CONCLUIDO` ou `EM_CONFERENCIA` | físico/evento; saldo só do liberado; pendência quando aplicável | estado/versão/escopo/saldo ou mapping ausente |
| `EM_CONFERENCIA` | concluir inspeção sem rejeição | `recebimentos.inspecionar` | versão atual; todos os retidos classificados | `CONCLUIDO` | entrada aceita, evento e resolução da pendência na mesma transação | total divergente, item externo ou conflito |
| `EM_CONFERENCIA` | concluir com rejeição | `recebimentos.inspecionar` | mesmas condições e justificativa | `COM_DIVERGENCIA` | somente aceitos entram; rejeitados e motivo permanecem; pendência resolve | justificativa ausente ou conflito |
| `CONCLUIDO`/`COM_DIVERGENCIA` | repetir conclusão | mesma | recebimento no tenant | inalterado | retorno idempotente, sem novo movimento | — |
| outro estado | concluir inspeção | mesma | — | inalterado | rollback | mensagem para recarregar |

## Implementação e consistência

A decisão usa `FOR UPDATE`, versão otimista e uma transação para itens, saldo, movimentos, cabeçalho, evento e pendência. O total aceito + rejeitado deve ser exatamente o que estava em conferência. O detalhe é sempre relido por tenant. A projeção transversal usa o mapping canônico UUID → tenant core e falha explicitamente quando ele não existe; não cria segunda máquina de estados. Abrir a rota revalida as políticas do controller e o estado no POST.

O relatório reutiliza consulta, filtros e escopo da central de recebimentos, limita exportação a 100 linhas e neutraliza células iniciadas por caracteres de fórmula. Totais consideram o período/filtros enviados e a data persistida do recebimento; divergências e concluídos são estados distintos. Não há regra de prazo configurada, portanto nenhum prazo fictício foi criado.

Não houve DDL: as tabelas, índices, permissão de inspeção, mapping e central já eram canônicos. Assim, migration, manifesto e consolidados foram preservados.

## Matriz de aceite e evidência

| Cenário | Pré-condição → ação → esperado | Resultado desta execução |
|---|---|---|
| Caminho principal | pedido e item inspecionável → registrar e concluir → detalhe final e estoque aceito | PASSOU por inspeção de código/teste estrutural; runtime BLOQUEADO |
| Entrada inválida | soma diferente/rejeição sem motivo → concluir → erro sem commit | PASSOU na validação implementada; runtime BLOQUEADO |
| Estado incompatível | estado não pendente → concluir → recusa sem efeito | PASSOU por guarda sob lock; runtime BLOQUEADO |
| Permissão | POST MVC/API direto sem política → 403 | PASSOU por atributos; autenticação runtime BLOQUEADA |
| Isolamento | tenant B consulta ID de A → 404/sem dados | PASSOU por SQL tenant-scoped; teste A/B runtime BLOQUEADO |
| Repetição | repetir conclusão final → mesmo estado, sem movimento | PASSOU por retorno idempotente; PostgreSQL BLOQUEADO |
| Concorrência | duas versões → uma conclui, outra recarrega | PASSOU por lock/versão; execução concorrente BLOQUEADA |
| Falha intermediária | falha em saldo/evento/pendência → rollback integral | PASSOU por transação única; injeção de falha runtime BLOQUEADA |
| Cancelamento/retificação | regra não documentada → não inventar comando | NÃO EXECUTADO; backlog explícito |
| Central | criar inspeção/concluir → pendência aberta/resolvida | PASSOU por SQL atômico; runtime BLOQUEADO |
| Indicadores | filtros → totais e registros de mesma consulta | PASSOU por implementação; banco BLOQUEADO |
| Exportação | mesmos filtros/permissão → CSV tenant-scoped neutralizado | PASSOU por implementação; download runtime BLOQUEADO |
| Template | lista/criação/detalhe → navegação e ajuda | PASSOU estaticamente; navegador BLOQUEADO |
| Banco | clean/upgrade/reaplicação | sem DDL novo; BLOQUEADO por ausência de PostgreSQL/psql |
| Regressão | build/testes existentes | BLOQUEADO por ausência do SDK 10.0.100 |

## Cobertura de ajuda e pendências

Foram conferidas e atualizadas somente três telas alcançáveis da jornada: central de recebimentos, novo recebimento e detalhe/conferência. As três possuem conteúdo específico para “Para que serve”, “Como funciona”, “Antes de começar”, “Regras importantes” e “Próximo passo”. O relatório recebeu resumo contextual próprio. Demais telas do módulo e da aplicação não foram declaradas cobertas.

Riscos restantes: prova real em PostgreSQL, dois tenants e duas sessões concorrentes; validação visual/teclado em celular, tablet e desktop; definição de negócio para devolução/estorno de rejeitados; atribuição individual da pendência (não há mapping canônico entre usuário UUID empresarial e usuário bigint, logo a fila fica setorial/transversal sem responsável individual).
