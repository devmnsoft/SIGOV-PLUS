# Status real dos módulos

Corte: 2026-09-15 (RC51.02Q); checkout `work` iniciado em `47b51a7a99888036dc1d3182b33d77121dc28a0c`, sem remoto/upstream configurado nesta execução.
Inventário documental: docs/inventario-modulos-sigov.md, docs/execucao/rc50_67_plano_homologacao_integrada_real.md e docs/roadmap/saas-industria-auditoria.md. Classificação conservadora: os 15 requisitos do contrato não foram demonstrados conjuntamente em runtime.

| Domínio/módulos existentes | Status | Evidência existente e lacuna de aprovação |
|---|---|---|
| Core, identidade, segurança, permissões | PARCIAL | Cookie compacto, snapshot request-scoped e policies pelo avaliador persistido; prova runtime PostgreSQL 16 e isolamento ponta a ponta pendentes |
| Central de trabalho e aprovações | PARCIAL | Totais independem do limite da lista, vencimento usa prazo/estado reais, GED sem fonte foi removido e pendências isolam tenant+usuário; prova runtime PostgreSQL 16 ainda pendente |
| Auditoria e LGPD | PARCIAL | Serviços/tabelas/rotas existentes; trilha e segregação runtime pendentes |
| SaaS, planos, contratação, administração global/cliente | PARCIAL | Catálogo/entitlements e SuperAdmin existentes; rotas administrativas de planos, módulos, assinaturas, flags e criação agora encaminham aos fluxos canônicos persistidos, sem clientes/KPIs fictícios; convites e ACEITE FUNCIONAL seguem bloqueados até evidência PostgreSQL 16 |
| Indústria Core | PARCIAL | Reserva considera `saldo - reservado`; saldo e movimento usam a mesma transação, lock de saldo e serialização da chave da operação; consumo não mascara reserva negativa, retry não repete movimento, estorno é limitado ao consumo e custo ausente falha sem recorrer ao preço de venda. Persistência da OP e estoque ainda ocorre em serviços/transações distintas e requer unificação antes da homologação |
| Indústria avançada | ESTRUTURA | Planejamento MRP/MPS/IoT; não há evidência de fluxo completo |
| Comercial, OS, manutenção industrial | PARCIAL | Parada industrial agora abre `manutencao_ordem_servico` real, idempotente e na mesma transação do vínculo/auditoria; execução, peças, inspeção final e prova PostgreSQL 16 seguem pendentes |
| Compras, licitações, contratos | PARCIAL | Migrations corretivas/FKs e controllers; convergência de banco não demonstrada |
| Almoxarifado, patrimônio, ativos | PARCIAL | Fluxos FUNC01/FUNC02; concorrência e isolamento runtime pendentes |
| Financeiro público, empresarial e tributário/NFS-e | PARCIAL | Controllers/services e operações parciais; transições e provedores não homologados |
| RH, folha, portal do servidor | PARCIAL | Contratos tipados e serviços existentes; validação integrada pendente |
| Educação | PARCIAL | Escola/turma/diário/portal existentes; criação de matrícula agora reserva a vaga antes do insert e valida aluno/escola/ano/turma no mesmo tenant e entidade; frequência exige matrícula elegível e período aberto. Transferência, correção auditada da chamada e prova PostgreSQL 16 continuam pendentes |
| Saúde e assistência social | PARCIAL | Serviços/rotas existentes; dados sensíveis e segregação exigem prova runtime |
| Frotas, manutenção, obras, fiscalização e engenharia | PARCIAL | Estruturas dos blocos existentes; fluxos e integração não homologados |
| Saneamento e meio ambiente | PARCIAL | Estruturas e serviços existentes; operações ponta a ponta pendentes |
| Processos, protocolo, ouvidoria e e-SIC | PARCIAL | Persistência parcial; transação/autorização/runtime pendentes |
| Jurídico | PARCIAL | Estrutura existente; filtros e alçadas de relatórios a auditar por fluxo |
| Agro e Campo/Geo | PARCIAL | Preservar serviços e UX existentes; falta evidência runtime atual dos 15 critérios |
| Integrações, mobilidade/offline, observabilidade | PARCIAL | Outbox, adapters e rotas; providers/isolamento/runtime pendentes |
| Legislativo, transparência, diário oficial, convênios, trânsito, defesa | PARCIAL | Inventários e controllers existentes; verificar cada fluxo após P0–P3 |
| GED e assinaturas | PARCIAL | Estrutura histórica existente; catálogo rebaixado para não promover GED na RC51.00; implementação bloqueada até última fase |

### Matriz executável RC51.02J

| Jornada | Estado real | Lacuna | Dependência | Critério de aceite |
|---|---|---|---|---|
| Fundação | Manifesto e consolidados preservados; wrapper Bash valida checksums normalizados/compatibilidades e recusa DDL sem ledger | Runtime não executado; execução Bash aguarda paridade com o runner canônico | SDK .NET 10, PowerShell e PostgreSQL 16 ausentes | clean install, upgrade autorizado e reexecução convergirem |
| SaaS Admin | Tenants/uso reais já existiam; cinco páginas paralelas eram fixas | Fluxos completos de usuário/cobrança ainda parciais | Serviços canônicos em `/Saas` | nenhuma rota SuperAdmin exibir cliente ou indicador inventado |
| Indústria/estoque | Integração existia sem transação e reservava sobre saldo bruto | OP e estoque ainda não compartilham unidade transacional | `IndustriaEstoqueService` e catálogo de entitlements | saldo 10/reserva 7 rejeitar reserva 4; retry não duplicar; rollback de saldo+movimento |
| Indústria/manutenção | A rota de parada devolvia um número obtido da sequência de OP, sem criar manutenção | Execução, peças e liberação do ativo continuam parciais | Contexto institucional e módulo `ordem_servico` | repetir a origem cria uma única OS real; outro tenant e entidade sem esfera válida falham |
| Central | Isolamento, totais e prazos corrigidos anteriormente | Prova concorrente/runtime pendente | `MinhaCentralService` e avaliador canônico | card e lista autorizada coincidirem em PostgreSQL 16 |
| Compras/recebimento | Serviços reais preservados | Recebimento parcial integrado não homologado | estoque canônico estabilizado | 10 receber 6+4 sem duplicação e com rollback |
| Educação — matrícula/frequência | Parcialmente implementado | Transferência ainda não cria novo vínculo preservando o anterior; índice histórico ainda limita vínculos simultâneos; correção de chamada não possui operação própria | Gate A, PostgreSQL 16 e catálogo canônico | duas requisições na última vaga resultam em uma matrícula; referência cruzada falha; chamada fora da vigência/ano encerrado falha; lançamento ausente não cria falta |
| Saúde/ACS | Parcialmente implementado, sem validação runtime nesta execução | escopo territorial, conclusão idempotente e encaminhamento/pendência requerem auditoria vertical | Gate A e autorização persistente | agendar e concluir uma vez, negar outro território e deduplicar encaminhamento |
| Jurídico histórico | Parcialmente implementado, sem validação runtime nesta execução | consulta histórica, profissional inativo, impressão limitada e anexo requerem auditoria vertical | Gate A e avaliador canônico | localizar passagem anterior sem duplicar processo e negar escopo/anexo indevido |

RC51.02J confirmou no checkout os indicadores/clientes fixos nas páginas SaaS Admin e as falhas de integridade da integração industrial descritas acima. As páginas paralelas foram removidas e suas rotas preservadas por redirecionamento aos fluxos canônicos. A reserva, consumo, entrada e estorno do saldo comercial passaram a ser atômicos internamente; a disponibilidade desconta reservas ativas e custo ausente não vira preço de venda ou zero. O parâmetro de lote/depósito já era persistido em `industria_producao_acabada` e foi preservado. A unidade transacional entre essa persistência da OP e o estoque continua pendente e impede promover o fluxo.

### Matriz da jornada operacional — RC51.02P

| Etapa | Implementação existente | Lacuna verificada | Dependência | Critério de aceite |
|---|---|---|---|---|
| Necessidade → requisição | Criação idempotente, edição de rascunho versionada, itens e vínculo opcional com OS | Vínculo direto com demanda industrial | modelo canônico da demanda industrial | uma necessidade confirmada gerar uma requisição e preservar a origem |
| Requisição → aprovação | Envio concorrente por `version`, detalhe real e histórico atômico | Política/alçadas não possuem configuração canônica identificada | parametrização persistida, sem aprovador fixo | devolução/decisão concorrente manter uma única versão válida |
| Aprovação → pedido | Tabelas legadas e permissões existentes | serviço/API/UI ainda ausentes | aprovação configurada e diferenças do fluxo público | repetição não duplicar pedido nem contornar licitação |
| Pedido → recebimento | Tabelas legadas e indicadores existentes | itens, aceite/rejeição, saldo e estorno não implementados no serviço | pedido emitido e estoque canônico | receber 6+4, rejeição não somar saldo e retry não duplicar |
| Recebimento → produção | estoque/indústria canônicos parciais | vínculo de rastreabilidade e qualidade ponta a ponta | recebimento por item e inspeção | somente material liberado entrar na disponibilidade recalculada |
| SaaS/navegação | entitlement e menus persistidos | prova runtime e consolidação do catálogo dual | Gate A PostgreSQL 16 | suspensão/revogação e troca de tenant falharem de modo fechado |

Nesta fatia, o contrato canônico de paginação (`Items`, `Page`, `PageSize`, `TotalItems` e `TotalPages`) passou a ser consumido corretamente pela listagem de requisições. Contagem e itens usam os mesmos filtros de tenant, exclusão lógica, situação, período e busca, com paginação no servidor e ordenação estável. A consistência é de leitura confirmada no instante de cada comando sob o isolamento padrão do PostgreSQL: uma alteração concorrente entre contagem e itens pode aparecer na consulta seguinte, sem impor transação pesada à listagem. A tela diferencia conjunto vazio de filtro sem resultado, preserva filtros na navegação e a edição transacional de rascunho valida a versão antes de substituir os itens. O detalhe e o envio continuam usando PostgreSQL, histórico e concorrência otimista. A caixa de aprovação permanece explicitamente pendente porque nenhuma fonte canônica de alçadas/aprovadores foi encontrada; não foi criada aprovação automática. O defeito CS8629 reportado já estava corrigido no checkout pela captura semântica de `entidadeId` e `usuarioId` obrigatórios antes da integração de manutenção e foi preservado.

RC51.02I removeu o onboarding hardcoded e o tenant fixo da Web; ausência de jornada agora é explícita e acesso cruzado é negado. Nenhum módulo foi promovido a FUNCIONAL, HOMOLOGADO ou PRODUCAO. RC51.02H corrigiu agregação/prazo/exercício e navegação contextual; a RC51.02G fechou a exposição transversal de pendências do mesmo tenant entre usuários e removeu o sucesso aparente da Minha Central quando contexto, schema ou banco estão indisponíveis; a fatia permanece sem validação runtime. RC51.02F decompôs a validação da correção `20260910120000` em cinco probes independentes. Não houve evidência runtime por ausência de `pwsh`, `.NET` e PostgreSQL 16. A evidência anterior da RC51.02C permanece histórica; upgrade legado formal e isolamento API profundo seguem pendentes. Indústria Core permanece PARCIAL. Dual catálogo Commercial/SaaS permanece. GED continua por último.
