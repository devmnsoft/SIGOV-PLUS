# FUNC05 — Educação e Gestão Escolar

## Escopo entregue

A trilha FUNC05 integra Educação ao SIGOV PLUS com autoridade PostgreSQL e operações Dapper já existentes, sem catálogo em memória: escolas, alunos e múltiplos responsáveis, professores, anos/períodos letivos, séries/etapas e componentes, turmas/quadro de horários, matrícula/enturmação/transferência, i-Diário, avaliações, ocorrências, pré-matrícula e portal interno.

A migration `20260824230000_func05_educacao_gestao_escolar.sql` cria as 21 tabelas `sigov.educacao_*`, chaves `bigint identity`, escopo tenant/entidade, checks, índices e unicidades. Regras críticas também são defendidas no banco: escola ativa, vaga/turma aberta, matrícula ativa única, período/ano aberto e intervalo de nota. Operações críticas mantêm histórico/auditoria antes/depois.

## Segurança e LGPD

A autorização usa as 28 permissões persistidas `educacao.*` e falha fechada. Documentos e metadados sensíveis não são apresentados integralmente em listas/CSV. O vínculo `educacao_portal_vinculo` associa usuário existente a aluno/responsável; não existe autenticação paralela. Exportações exigem `educacao.exportar`, aplicam máscara e neutralização CSV.

## Rotas

MVC: `/Educacao`, `/Educacao/Escolas`, `/Educacao/Escolas/Nova`, `/Educacao/Alunos`, `/Educacao/Alunos/Novo`, `/Educacao/Alunos/Detalhe/{id}`, `/Educacao/Responsaveis`, `/Educacao/Professores`, `/Educacao/Professores/Novo`, `/Educacao/AnosLetivos`, `/Educacao/SeriesEtapas`, `/Educacao/Turmas`, `/Educacao/Turmas/Nova`, `/Educacao/Turmas/Detalhe/{id}`, `/Educacao/Matriculas`, `/Educacao/Matriculas/Nova`, `/Educacao/Frequencias`, `/Educacao/Frequencias/Lancar`, `/Educacao/Avaliacoes`, `/Educacao/Avaliacoes/LancarNotas`, `/Educacao/Ocorrencias`, `/Educacao/PreMatriculas`, `/Educacao/PreMatriculas/Nova` e `/Educacao/PortalAluno`.

API: dashboard, CRUD/listagens de escolas/alunos/professores/turmas/matrículas, frequência, avaliações/notas, pré-matrícula/conversão/indeferimento, boletim e exportações sob `/api/educacao`.

## Fluxos e regras

* Matrícula ativa é única por aluno/escola/ano/série; enturmação exige escola e turma abertas e saldo de vagas; cancelamento exige justificativa e transferência produz histórico.
* Frequência só aceita professor vinculado/perfil autorizado, data dentro do ano e período aberto; falta justificada exige motivo.
* Nota/conceito/parecer segue a metodologia da etapa, o intervalo da avaliação e bloqueios de fechamento; alterações são auditadas.
* Pré-matrícula recebe protocolo único; conversão transacional verifica vaga e cria matrícula, e ausência de vaga permite somente lista de espera.
* Dashboard e CSV consultam PostgreSQL no escopo corrente; informações pessoais são mascaradas.

### Matriz de continuidade (revisão 2026-09-16)

| Jornada | Implementação existente | Lacuna confirmada | Mudança desta revisão | Evidência automatizada |
|---|---|---|---|---|
| Contexto e cadastros | `escola`, `ano_letivo`, `turma`, `aluno`, `matricula`, `professor_turma` e autorização do serviço canônico | A interface ainda permite alguns identificadores livres em vez de seletores pesquisáveis | Nenhuma estrutura paralela foi criada; os lançamentos continuam referenciando os cadastros canônicos | `EducacaoModuleSmokeTests.Repository_Usa_Dapper_Parametrizado_Tenant_Entidade_Auditoria_Outbox` |
| Frequência | API, serviço Dapper, matrícula e ano letivo | Presença era o valor padrão; justificativa ausente era fabricada; atribuição do professor não era verificada no `INSERT` | Estado inicial agora é `NAO_LANCADO`; lançamento exige estado explícito, justificativa real, matrícula vigente, data no ano letivo e atribuição turma/componente | `EducacaoApiTests.Frequencia_Nao_Assume_Presenca_Nem_Inventa_Justificativa` e smoke de repositório |
| Avaliação e resultado | `avaliacao`, `nota`, endpoints e auditoria em `educacao_evento` | Escala/peso tinham padrão oculto; nota dependia principalmente de constraint | Escala e peso passam a ser explícitos; avaliação valida atribuição/data/estado e resultado valida elegibilidade, avaliação aberta e limite | smoke de repositório |
| Cálculo e boletim | Consulta de boletim | A consulta inventava corte de 60% e média aritmética, sem política acadêmica persistida | Removidas inferências: zero continua valor lançado, ausência vira `NAO_LANCADO` e a média permanece indisponível até existir política versionada | `EducacaoModuleSmokeTests.Boletim_Nao_Inventa_Regra_De_Aprovacao_Sem_Politica` |
| Fechamento/reabertura | Diário Bloco 3 verifica conteúdo/frequência e grava histórico com justificativa | Ainda não existe política acadêmica versionada nem snapshot publicável de resultados; a conferência não cobre todos os alunos elegíveis | Mantido como parcial e sem declarar fechamento acadêmico completo | testes existentes de estrutura do Bloco 3 |
| Painel, Minha Central e relatórios | dashboard e exportações básicas | Contagens ainda não oferecem todo o recorte escola/ano/permissão; não há integração completa com Minha Central | Sem mudança nesta revisão; não foi criada tabela paralela de tarefas | pendência registrada |
| Ingresso e vagas | Pré-matrícula simples e criação direta de matrícula | Não havia rascunho, transições validadas, concorrência otimista, oferta/reserva nem conversão idempotente | A migration `20260916210000` acrescenta versão, oferta, pendência e origem; serviço/API validam estados, motivos, validade e última vaga; matrícula pode aguardar turma e a enturmação tem vigência própria | `EducacaoModuleSmokeTests.Ingresso_E_Vagas_Preserva_Concorrencia_Origem_E_Enturmacao_Opcional` |

### Regras e fontes adotadas

* Ano letivo é obtido de `ano_letivo.data_inicio/data_fim`; a data corrente do servidor não substitui esse intervalo nos lançamentos.
* Elegibilidade de frequência deriva da matrícula na turma e da data da matrícula. A atribuição deriva de `professor_turma` e do componente informado.
* Escala máxima e peso são dados explícitos da avaliação. Este documento **não** define média mínima, percentual mínimo de frequência, precisão, arredondamento, recuperação ou equivalência de conceitos.
* Pré-matrícula e análise não ocupam capacidade. Ofertas `OFERTADA` ou `ACEITA`, ainda válidas, reservam capacidade; a conversão consome somente a oferta de origem e a restrição única torna a repetição segura. A oferta é contabilizada no escopo escola/ano/série/turno e a alocação em turma permanece uma etapa separada quando a turma ainda não foi informada.
* Não há classificação automática: pontuação preexistente não decide a fila. Aprovação, indeferimento, complementação e cancelamento são decisões administrativas autorizadas, versionadas e, quando afetam o solicitante, exigem motivo.
* Na ausência de política acadêmica persistida e versionada, média e situação final ficam indisponíveis. Isso é impedimento funcional, não zero nem aprovação implícita.

### Evolução de Interface, Regras e Templates (Revisão 2026-09-19)

Nesta evolução, a trilha FUNC05 foi alinhada integralmente ao padrão arquitetural e visual estabelecido nas trilhas FUNC01 a FUNC04:

* **Padrão visual unificado:** Breadcrumb canônico (`Início > Educação > [Página]`), título H1 único por tela, bloco retrátil `<details class="how-to">` com orientações operacionais em todas as views alvo, e ícones SVG canônicos do sistema SIGOV (sem classes de fontes externas como `bi-`).
* **Eliminação de IDs técnicos manuais:** Todos os formulários operacionais substituíram inputs numéricos de ID por seletores pesquisáveis (`<select>`) vinculados aos catálogos persistidos por nome (escolas ativas, anos letivos, turmas com indicação de saldo de vagas, alunos com nome e documento mascarado, professores e cursos).
* **Bloqueios e autoridade no serviço:**
  - **Escola e ano:** Escola inativa não recebe matrícula nova, enturmação nem pré-matrícula. Ano letivo respeita `data_inicio/data_fim` persistidos em banco.
  - **Turma e vagas:** Turma fechada recusa enturmação. Saldo de vagas é validado rigorosamente no serviço (capacidade menos matrículas ativas menos ofertas reservadas).
  - **Matrícula:** Matrícula ativa única por aluno + escola + ano + série. Cancelamento exige justificativa formal não vazia gravada no histórico. Transferência garante que não fiquem duas ativas simultaneamente.
  - **Ingresso e pré-matrícula:** Inscrição gera protocolo e não ocupa capacidade. Oferta válida (`OFERTADA`/`ACEITA`) reserva capacidade. Conversão transacional consome apenas a oferta de origem (idempotente). Esgotada a capacidade, direciona exclusivamente à lista de espera.
  - **Diário de frequência:** Estado inicial `NAO_LANCADO`. Nunca assume presença por omissão. Lançamento exige professor com atribuição na turma/componente (`professor_turma`), data dentro do ano letivo e período aberto. Falta justificada exige motivo real.
  - **Avaliações e boletim:** Escala máxima e peso explícitos da avaliação. Avaliação fechada recusa notas. **Sem política acadêmica persistida e versionada em banco, o sistema não calcula médias aritméticas, notas de corte ou aprovação implícita.** Exibição explícita de "média indisponível até haver política". Ausência de nota aparece como `NAO_LANCADO`, nunca como zero.
  - **Dashboard e CSV:** Dashboard com dados reais no escopo tenant/entidade e painel de "Regras vigentes (B3, B4, B5, B6)". Exportações CSV com proteção anti-fórmula (`=+-@\t\r`), codificação UTF-8 BOM, limite explícito e documentos mascarados (LGPD).
  - **Isolamento de Escopo:** Sem ponte, referência ou dependência com FUNC06 (Saúde).

### Pendências deliberadamente não declaradas como concluídas

1. Modelar política acadêmica multi-esfera versionada, incluindo escala numérica/conceitual, precisão, arredondamento, recuperação e publicação, mediante decisão de produto.
2. Unificar o diário base e o diário Bloco 3 sem migração destrutiva, preservando históricos já publicados.
3. Completar conferência atômica, snapshot do fechamento/publicação, correção versionada e concorrência otimista.
4. Integrar os agregados autorizados à Minha Central e concluir relatórios/impressão responsivos.
5. Validar navegador e PostgreSQL 16 quando esses runtimes estiverem disponíveis. Compras e Estoque permanecem fora desta revisão.

GED/InovaGED foi explicitamente adiado para a etapa final. FUNC05 não promove a RC50.68, que continua **BLOCKED** por runtime/CI/PostgreSQL oficiais, e não inicia nem marca a RC50.69.

## Auditoria operacional do ciclo 2026-09-22

Baseline auditada: branch `work`, commit `4824442`. A árvore estava limpa antes da
alteração. Foram inspecionados os fluxos MVC/API/aplicação/Dapper das escolas, anos
letivos, turmas, alunos, professores, matrículas, pré-matrículas, frequência,
avaliações e boletim; as migrations `021_educacao_base.sql`,
`20260824230000_func05_educacao_gestao_escolar.sql` e
`20260916210000_educacao_ingresso_vagas.sql`; o manifest; as views e scripts do
núcleo; e os testes existentes de Educação. Transporte, merenda, biblioteca,
integrações oficiais e GED não foram auditados funcionalmente neste ciclo.

| Capacidade | Estado e evidência | Lacuna/risco observado | Regra e origem | Alteração/validação deste ciclo |
|---|---|---|---|---|
| Configuração escolar | **Parcial**: escola, ano, curso/série, turma e professor persistem no schema canônico e passam pelo guard de permissão | Períodos e componentes ainda não formam um cadastro versionado único; carregamento MVC ocultava indisponibilidade | Falha de schema/configuração deve ser explícita (regra do repositório) | A carga de seletores registra o erro e bloqueia a efetivação em vez de simular catálogo vazio; build ficou bloqueado pela ausência do SDK |
| Vagas e ingresso | **Implementada com evidência estática**: oferta, validade, estados, espera e conversão transacional estão na migration `20260916210000` | PostgreSQL concorrente não pôde ser executado neste ambiente | Oferta válida reserva; pré-matrícula não ocupa; fonte: configuração/migration vigente | Preservada; reaplicação e corrida real permanecem bloqueadas sem PostgreSQL |
| Matrícula/enturmação | **Parcial, corrigida**: reserva condicional de vaga, contexto, enturmação e auditoria existiam | Duas criações concorrentes do mesmo aluno podiam passar pela leitura de duplicidade; número direto usava relógio; cancelamento repetido liberava mais de uma vaga | Idempotência, última vaga e histórico (requisito unificado); sequência já publicada é autoridade vigente | Trava consultiva transacional por aluno/contexto, sequência PostgreSQL na mesma transação, status inicial controlado pelo servidor e cancelamento idempotente com `FOR UPDATE` |
| Transferência | **Implementada com evidência estática**: origem e destino são travados, vaga é movida e nova matrícula referencia a origem | Equivalência curricular não configurada; cenário real não executado | Não transportar notas/frequência implicitamente (requisito unificado) | Preservada; não foi inventada equivalência |
| Diário/frequência | **Parcial**: elegibilidade por vigência, atribuição professor/turma/componente e estados explícitos estão no SQL | Diário base e Bloco 3 ainda são modelos paralelos; lote/revisão e conflito de edição não foram concluídos | Regras documentadas na revisão de 2026-09-16 | Preservado; não declarado concluído |
| Avaliações/resultados | **Parcial**: escala, peso e limite são validados; ausência não vira zero | Falta política acadêmica versionada para arredondamento, recuperação e cálculo final | Decisão de produto pendente, registrada na documentação canônica | Nenhum critério pedagógico foi inventado |
| Fechamento/reabertura | **Parcial** no Bloco 3 | Sem snapshot acadêmico publicável, conferência integral ou versão anterior homologada | Requisito unificado; depende de decisão da política acadêmica | Não alterado; permanece bloqueado para conclusão acadêmica |
| Boletim/portal | **Parcial**: consulta autorizada lista lançamentos e explicita `NAO_LANCADO` | Sem política/snapshot não há média nem documento definitivo; impressão não foi validada | Ausência não equivale a zero; política pendente | Preservado sem resultado fictício |
| Pendências/indicadores | **Parcial**: dashboard usa agregados reais por tenant/entidade | Recortes de escola/ano e integração completa com Minha Central ausentes | Requisito unificado | Não ampliado antes do núcleo |
| Template/ajuda | **Parcial**: núcleo possui breadcrumb e ajuda específica | Nem todas as views genéricas do Bloco 3 possuem orientação específica | Requisito de usabilidade deste ciclo | Matrícula agora filtra ano/turma pelo contexto escolhido, remove ID técnico visível e anuncia falha persistente |

### Rastreabilidade do fluxo principal inspecionado

`_Sidebar` → rotas de `EducacaoController` → views `PreMatriculas` e
`Matriculas` → `PreMatriculasController`/`MatriculasController` da API →
`EducacaoService` (guard persistente) → `EducacaoRepository` → tabelas
`pre_matricula_inscricao`, `educacao_oferta_vaga`, `matricula`, `turma` e
`educacao_evento`. O isolamento verificado no código usa `tenant_id` e
`entidade_id` em leituras e mutações; escola/turma/ano são novamente conferidos
no SQL. Não foi declarado isolamento homologado porque não houve banco executável.

### Matriz dos cenários mínimos desta revisão

| Cenário | Esperado | Método | Observado |
|---|---|---|---|
| Matrícula válida | persistir no contexto e consumir uma vaga | leitura de serviço/repositório e teste estrutural existente | **PASSOU (estático)**; execução SQL bloqueada |
| Duplicidade | repetição não criar vínculo nem consumir vaga | inspeção da trava consultiva e busca após a trava | **PASSOU (estático)** |
| Última vaga | atualizações concorrentes não excederem capacidade | `UPDATE ... vagas_ocupadas < capacidade` | **PASSOU (estático)**; corrida real bloqueada |
| Enturmação inválida | nenhuma alteração parcial | CTE transacional e contagem igual a um | **PASSOU (estático)** |
| Transferência | preservar origem e vigências | inspeção da transação e eventos | **PASSOU (estático)** |
| Frequência | respeitar vigência na data | inspeção do predicado entre matrícula/enturmação/calendário | **PASSOU (estático)** |
| Registro ausente | não virar falta/zero | contratos, SQL e teste existente | **PASSOU (estático)** |
| Avaliação | escala/peso persistidos, sem cálculo arbitrário | serviço, SQL e teste existente | **PASSOU (estático)**; arredondamento **BLOQUEADO** por política |
| Fechamento | bloquear pendências e versionar resultado | inspeção do Bloco 3 | **FALHOU para fechamento acadêmico completo** |
| Reabertura | exigir permissão/justificativa e preservar versão | busca no Bloco 3/schema | **FALHOU para snapshot acadêmico completo** |
| Acesso | professor/responsável limitados ao vínculo | guards e joins inspecionados | **PASSOU (estático)**; HTTP real bloqueado |
| Isolamento | não cruzar tenants/entidades/escolas | predicados SQL inspecionados | **PASSOU (estático)**; dois tenants reais bloqueados |
| Interface | seletores, contexto, mensagens e impressão | inspeção Razor/JS | filtros/mensagens **PASSARAM (estático)**; navegador/impressão **NÃO EXECUTADOS** |
| Migração | instalação, upgrade e reaplicação compatíveis | manifest e scripts inspecionados | sem schema novo neste ciclo; PostgreSQL **BLOQUEADO** |
| Regressão | preservar componentes impactados | `git diff --check` e testes existentes | diff **PASSOU**; testes .NET **BLOQUEADOS** sem SDK |

Decisões ainda indispensáveis: política acadêmica versionada por esfera/etapa,
unidade oficial de frequência, regras de recuperação/arredondamento/classificação
e contrato de snapshot/reabertura. Até serem parametrizadas, fechamento definitivo,
média e situação final continuam indisponíveis, em vez de receber valores arbitrários.

## Jornada de conferência e fechamento versionado (22/09/2026)

A conferência canônica do diário está em `GET /api/educacao/diario-classe/{id}/conferencia`. Ela usa aulas não canceladas, matrículas da mesma turma/ano letivo cuja data de ingresso alcança o período observado, conteúdo e frequência persistidos. Matrículas canceladas ou transferidas não são inferidas como vigentes: quando o histórico disponível não permite provar a vigência, o caso deve ser corrigido na matrícula antes do fechamento. Falta de lançamento é pendência e nunca é convertida em falta; aulas canceladas não compõem os totais.

A prévia é somente leitura e produz um token SHA-256 dos dados conferidos. O fechamento bloqueia o diário, recalcula a mesma conferência dentro da transação e rejeita token divergente. Em sucesso grava snapshot, versão, autor, instante e histórico atomicamente. Repetição sobre diário já fechado devolve o fechamento vigente sem duplicar versão. A reabertura exige permissão e justificativa, preserva o snapshot anterior e o fechamento posterior referencia a versão precedente como retificação.

### Inventário verificado da jornada

| Etapa | Classificação | Evidência e limite |
|---|---|---|
| consultar turma/período | IMPLEMENTADA COM EVIDÊNCIA | diário persistente, consulta tenant-scoped e tela de conferência |
| identificar pendências | IMPLEMENTADA COM EVIDÊNCIA | conteúdo e frequência esperada por aluno/aula são detalhados com ação de correção |
| corrigir lançamentos | PARCIAL | atalhos existentes levam a conteúdo/frequência; notas do diário ainda não possuem vínculo canônico com `nota` |
| validar resultados | PARCIAL | consistência de diário/frequência é revalidada; política acadêmica versionada para média/aprovação continua ausente |
| fechar período | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | transação, lock, token de concorrência, snapshot e idempotência; execução PostgreSQL depende de ambiente |
| emitir boletim definitivo | PARCIAL | consulta de lançamentos preservada, sem inventar média; emissão vinculada ao snapshot ainda depende da política acadêmica |
| reabrir/retificar | IMPLEMENTADA COM EVIDÊNCIA ESTÁTICA | permissão específica, justificativa, histórico e encadeamento de versões |
| central de pendências | PARCIAL | pendências do diário refletem banco; deduplicação transversal da central não foi alterada |

O contrato `IEducacaoSequencialService.ProximoAsync` permanece não nulo. A consulta usa `ExecuteScalarAsync<string?>`, valida `null`/vazio e lança inconsistência explícita, sem `null!` ou supressão; indisponibilidade do banco continua propagada como falha. Assim, o CS8603 anteriormente associado ao retorno foi corrigido de acordo com a obrigatoriedade do número sequencial.

### Limites não mascarados

Não foram criadas nota de corte, média aritmética, equivalência curricular ou aprovação implícita. Até existir política acadêmica persistida e versionada, o boletim continua parcial, distingue zero de `NAO_LANCADO` e não pode ser promovido a documento acadêmico definitivo. A execução limpa/upgrade/reaplicação da migration requer PostgreSQL 16 e deve ser registrada como bloqueada quando `psql` não estiver disponível.

## Transição de ano letivo e rematrícula (22/09/2026)

A jornada canônica passou a reutilizar `matricula`, `turma`, `ano_letivo`, capacidade e histórico já existentes. A tela `/Educacao/Rematriculas` guia origem/destino, candidatos, pendências, revisão, confirmação e resultado. `GET /api/educacao/rematriculas/simulacao` é somente leitura; `POST /api/educacao/rematriculas/confirmacao` executa o conjunto explicitamente marcado, e `GET /api/educacao/rematriculas/operacoes` apresenta o histórico no mesmo escopo de tenant e entidade.

Resultado final e progressão são fontes persistidas explícitas (`educacao_resultado_final` e `educacao_progressao_config`). Não há progressão por número ou nome de turma. Resultado ausente/não publicado gera `DEPENDENTE_ANALISE`; regra ou oferta ausente gera `SEM_OFERTA_COMPATIVEL` com “destino não definido”; matrícula de destino existente gera `JA_REMATRICULADO`. Reprovação não exclui automaticamente: ela só se torna elegível quando existe configuração autorizada para o resultado.

A confirmação é transacional por operação e usa savepoint por item, portanto o lote admite resultado parcial explícito. Cada item relê e bloqueia origem, resultado e turma; compara o token da prévia, impede duplicidade rastreável, verifica a última vaga, cria uma nova matrícula com `origem_matricula_id`, incrementa capacidade e relê o registro persistido. Notas, faltas e resultado não são copiados. A chave idempotente é vinculada ao SHA-256 do conteúdo e não aceita reutilização com carga diferente.

O cancelamento de matrícula passou a bloquear quando há frequência, notas ou documentos dependentes, preservar o registro e justificar o procedimento. A vaga é liberada uma vez, pois repetição sobre estado `CANCELADA` é idempotente.

### Auditoria inicial e classificação

| Recurso | Classificação | Evidência/limite verificado no código e SQL |
|---|---|---|
| CS8603 do sequencial | IMPLEMENTADO COM EVIDÊNCIA | contrato não nulo, leitura anulável e falha explícita; sem `null!` |
| registro inexistente | IMPLEMENTADO COM EVIDÊNCIA | serviço retorna “Registro não encontrado” e detalhe MVC retorna 404 |
| fechamento acadêmico | IMPLEMENTADO COM EVIDÊNCIA ESTÁTICA | conferência, token, lock, snapshot e versão; PostgreSQL não executado neste ambiente |
| reabertura/retificação | IMPLEMENTADO COM EVIDÊNCIA ESTÁTICA | justificativa, histórico e vínculo da versão retificada |
| boletim vinculado ao resultado correto | PARCIAL | lançamentos reais e ausência explícita preservados; política de média/aprovação ainda não foi definida |
| preparação da oferta | IMPLEMENTADO COM EVIDÊNCIA | escola/ano/turma/status/capacidade persistidos e revalidados |
| simulação e elegibilidade | IMPLEMENTADO COM EVIDÊNCIA ESTÁTICA | consulta única, motivos, token e nenhum `INSERT`/reserva |
| confirmação e histórico | IMPLEMENTADO COM EVIDÊNCIA ESTÁTICA | função PostgreSQL, lock, origem rastreável, idempotência e operação por item |
| cancelamento/correção | PARCIAL | cancelamento seguro implementado; troca de destino com dependências continua exigindo procedimento autorizado existente |
| integração à central transversal de pendências | PARCIAL | pendência aparece na jornada; central transversal não ganhou tabela paralela nem integração nova |

### Cenários de aceite e evidência esperada

| Cenário | Resultado esperado | Evidência automatizável |
|---|---|---|
| elegível | nova matrícula, vínculo de origem e vaga +1 | função e índice de origem |
| destino ausente | pendência clara e zero gravação | classificação `SEM_OFERTA_COMPATIVEL` |
| resultado pendente | análise, sem aprovação presumida | `DEPENDENTE_ANALISE` |
| simulação | nenhuma matrícula/vaga alterada | endpoint chama somente consulta |
| última vaga concorrente | um item conclui; concorrente falha | `FOR UPDATE` e capacidade revalidada |
| reenvio | resposta anterior ou conflito de conteúdo, sem duplicidade | chave + `request_hash` + índice |
| lote parcial | contagens e motivo por item; retentativa segura | savepoints e itens persistidos |
| mudança após prévia | conflito solicita nova simulação | token recalculado sob lock |
| cancelamento com dependências | impedimento e procedimento explicado | contagem de frequência/notas/documentos |
| outro tenant/escola | registro não localizado, sem vazamento | filtros de tenant/entidade/escola |
| histórico | origem/destino e operador preservados | operação, itens e `origem_matricula_id` |
| template | seis etapas, ajuda, contexto e seleção explícita | view e JavaScript do módulo |

**Limite de validação:** restore/build/testes e cenários transacionais reais permanecem **BLOQUEADOS** quando o SDK .NET 10 ou PostgreSQL 16 não estão instalados. Evidência estática não é homologação.
