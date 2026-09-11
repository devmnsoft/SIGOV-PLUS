# RC51.02H — Minha Central e navegação contextual

Data: 2026-09-11. Checkout inicial: branch `work`, HEAD `ac06642902bab7b52373cbe4473432a349cf8967`, árvore limpa e sem remoto/upstream configurado.

## Causas confirmadas

- O total de pendências era o tamanho da lista limitada a oito; vencimento era inferido pela palavra `prazo` no título.
- O indicador documental inferia GED pelo nome da entidade na auditoria, sem estado operacional legítimo.
- A tela mostrava o ano do relógio, mesmo quando outro exercício estava selecionado.
- Falha de contexto e falha de infraestrutura convergiam para uma coleção vazia e a mensagem de sucesso “Nenhuma pendência”.
- Favoritos eram botões dentro de links. Favoritos e recentes usavam chaves globais, sem usuário ou contexto, e recentes eram renderizados com `innerHTML` sem validar URL.
- A busca de comandos substituía indisponibilidade do backend por catálogo local hardcoded.
- A rota ativa usava prefixo textual simples, permitindo colisões entre destinos.

## Matriz de execução

| Jornada | Lacuna | Arquivos | Dependências | Teste | Resultado |
|---|---|---|---|---|---|
| Minha Central | agregação limitada e prazo inferido do título | `MinhaCentralService`, view model e view | snapshot persistente; PostgreSQL | inspeção SQL; build pendente | total e vencidas calculados pelo banco antes do `LIMIT`, por tenant, usuário, status e prazo |
| Minha Central | GED fictício e exercício do relógio | view e serviço | exercício do contexto autorizado | inspeção Razor/SQL | card GED removido; exercício selecionado resolvido pelo identificador persistido |
| Minha Central | indisponibilidade apresentada como vazio | controller e view | correlation id | inspeção dos estados HTTP/Razor | 403 e 503 distintos; vazio só aparece após carga bem-sucedida; recarga e referência de suporte disponíveis |
| Favoritos | ação aninhada no link e estado global | sidebar e `sigov-ui.js` | identidade e contexto do layout | `node --check` | botão irmão, `aria-pressed`, chave por usuário/tenant/entidade/exercício e clique sem navegação |
| Recentes | compartilhamento, URL não confiável e HTML executável | layout, `sigov-ui.js`, `minha-central.js` | links autorizados renderizados no request | `node --check` | legado descartado, URL same-origin validada, visibilidade revalidada e texto criado com `textContent` |
| Paleta | fallback produtivo e HTML dinâmico | `sigov-command-palette.js` | `/Busca/Sugestoes` | `node --check` | falha explícita, sem catálogo paralelo, URLs internas e nós DOM seguros |
| Navegação ativa | colisão de prefixos | sidebar e `sigov-ui.js` | rota atual | inspeção Razor/JS | comparação por segmento; único destino recebe `aria-current`; grupo pertinente é expandido |

## Limites e continuidade

Não foi criada migration nem alterado schema. O ambiente não possui SDK .NET, PowerShell, PostgreSQL 16 nem navegador; portanto build, suites, runtime, banco vazio/upgrade/reexecução, screenshots e larguras 390/768/1366/1920 permanecem bloqueados. SaaS, Indústria e módulos seguintes não foram promovidos: a sequência do contrato exige fechar o gate runtime P0 antes de avançar.

Próximo item exato: em ambiente com .NET 10 e PostgreSQL 16, executar os gates obrigatórios e provar com dois usuários e dois tenants que total/lista, prazo, contexto, troca, revogação, logout e suspensão permanecem isolados; aceite quando as consultas de referência coincidirem com os indicadores e nenhuma entrada recente sobreviver no contexto indevido.
