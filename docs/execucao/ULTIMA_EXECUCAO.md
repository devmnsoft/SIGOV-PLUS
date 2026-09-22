# Última execução

Data: 2026-09-22. RC51.03A — retry concorrente do recebimento de compras. Estado: **CORRIGIDO ESTATICAMENTE / GATE RUNTIME BLOCKED**.

- Baseline: branch `work`, HEAD inicial `7443a8a4b7cb62173e6bb28083dbdaca9ffd3a77`, árvore limpa. O fluxo selecionado foi rastreado da navegação de Compras Empresariais às rotas MVC, application service, repositório Dapper, tabelas de recebimento/eventos e efeitos em saldo/movimento de estoque.
- Defeito confirmado por inspeção da ordem transacional: dois comandos simultâneos com a mesma chave podiam ambos observar ausência antes do lock do pedido; após o primeiro commit, o segundo avaliava a versão já incrementada e falhava, em vez de devolver o recebimento persistido. O repositório agora repete a leitura da chave após adquirir o lock e antes de validar estado/versão.
- O teste de regressão estático foi acrescentado à classe existente `PostBuild01RegressionTests` e exige as leituras de idempotência antes e depois de `FOR UPDATE`; nenhuma classe de teste nova foi criada. Não houve alteração de schema, migration, scripts consolidados, permissão, regra financeira ou interface.
- PASSOU: catálogo/paridade estática das migrations, conflitos de rotas, colisões SQL, artefatos rastreados, sintaxe Bash/JSON, JavaScript e `git diff --check`.
- **BLOCKED:** restore, build, teste .NET, PostgreSQL 16, concorrência real, dois tenants, Web/API, navegação e capturas; não há `dotnet`, `psql`, PowerShell, connection string ou aplicações iniciadas. A correção não é declarada homologada.
- Próximo incremento: provisionar o Gate A, executar duas confirmações simultâneas da mesma chave e chaves distintas sobre o mesmo saldo, e só então concluir a decisão de inspeção/devolução/estorno já registrada no backlog RC51.03.

---

Data: 2026-09-16. RC51.03 — recebimento parcial de compras empresariais. Estado: **IMPLEMENTADO SEM HOMOLOGAÇÃO RUNTIME**.

- Central real com filtros, paginação no servidor, ordenação estável e totais tenant-scoped; formulário por pedido com destino nominal, quantidades e rastreio opcional.
- Confirmação serializa pedido/itens, bloqueia excesso e estado incompatível, é idempotente, audita e grava entrada no estoque canônico apenas para quantidade aceita. Item sujeito a inspeção permanece em conferência e indisponível.
- Migration forward-only `20260916120000` sincronizada com manifesto e scripts; novas tabelas usam bigint identity e preservam os UUIDs legados.
- **BLOCKED:** .NET, PostgreSQL 16 e navegador ausentes; build, testes runtime, concorrência e evidências visuais não executados. Documento e matriz: `docs/entregas/RC51-03-RECEBIMENTO-COMPRAS.md`.
- Próximo item exato: executar Gate A e cenários PostgreSQL; depois concluir decisão de conferência, devolução e estorno com verificação de movimentos posteriores.

---

Data: 2026-09-16. Correção de nulabilidade do ciclo contratual SaaS. Estado: **CORRIGIDA ESTATICAMENTE / GATE RUNTIME BLOCKED**.

- Causa dos dois `CS8602`: `QuerySingleOrDefaultAsync<ContractRow>` admite ausência; a guarda composta para suspensão/reativação não estreitava `before` para as guardas posteriores de vigência e cancelamento. O fluxo agora entra em um bloco de mutação de contrato existente, retorna antes de qualquer `UPDATE` quando a linha não existe e usa a variável local não nula `currentContract`, sem `!`, `?.`, pragma ou mudança do contrato público.
- Semântica preservada: entrada inválida, módulo ausente, tenant ausente/inativo, tenant suspenso/cancelado, dependência ausente, contrato ausente, estado incompatível, versão concorrente, vigência encerrada e cancelamento efetivado falham antes da gravação. O controller continua negando falta de identidade/permissão e acesso de administrador local a outro tenant antes de chamar o serviço.
- Matriz: contratar → SuperAdmin autorizado → tenant ativo e módulo canônico/dependências válidos → insere contrato e auditoria → `CONTRATADO`; suspender → SuperAdmin autorizado → contrato vigente em estado elegível e versão atual → preserva condições, audita → `SUSPENSO`; reativar → SuperAdmin autorizado → contrato `SUSPENSO`, tenant ativo, vigência/cancelamento elegíveis e versão atual → preserva condições, audita → `HABILITADO`.
- Formulários no escopo: lista/detalhe são `GET` autorizados e leem via Dapper; contratar, suspender e reativar são `POST` com antiforgery, autorização persistente, validação no serviço, transação, auditoria e redirecionamento sem afirmar sucesso quando o resultado falha. Não houve alteração visual nem de schema nesta correção; o template e as jornadas persistidas existentes foram preservados.
- Teste de regressão existente foi ampliado, sem criar classe, para exigir a guarda explícita e impedir o retorno do operador de supressão. `git diff --check` e varredura de conflitos passaram.
- **BLOCKED:** `dotnet restore/build/test`, compilação Razor, PostgreSQL 16, navegador e capturas não foram executados porque este container não dispõe de `dotnet`, `psql`, Docker nem navegador. A entrega não é declarada homologada.
- Risco restante e próximo item exato: instalar o SDK `10.0.100` e disponibilizar PostgreSQL 16 isolado; executar os gates da solução e provar contratação/suspensão/reativação, concorrência, rollback, duplicidade, dois tenants, permissão negada, sessão revogada e preservação do espelho legado; depois executar a jornada autenticada e as seis larguras.

---

Data: 2026-09-15. Correção do ciclo contratual SaaS. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

| Item inspecionado | Classificação | Evidência/limite |
|---|---|---|
| Catálogo e contratação | parcial | `modulo_saas` e `tenant_modulo_contratado` são autoridades persistidas; catálogo Commercial legado ainda é consumidor de navegação, não autoridade contratual |
| Administração de tenants/SuperAdmin | implementado sem validação | pesquisa, filtros, paginação, detalhe, vínculos e contratos usam Dapper; runtime indisponível |
| Usuários, vínculos, perfis e permissões | parcial | identidade/sessão e autorização persistidas existem; jornada completa de convite e limite concorrente não foi homologada |
| Sessões e avaliador | implementado sem validação | validação de sessão e avaliador persistente preservados; mudança P0/P1 fora desta correção |
| Sidebar, layout e formulários | parcial | shell compartilhado preservado; confirmação contratual agora explicita cliente, módulo, efeito e consequências |
| Auditoria de formulários | bloqueado | 591 candidatos e zero jornadas homologadas conforme checkpoint vigente |
| Manutenção, inspeção e não conformidades | parcial/ausente | execução e histórico de OS parciais; inspeção e não conformidade ligadas à OS continuam ausentes |

- Estado inicial: branch `work`, HEAD `f1b8a87bcc50aec60e4a67d25a9493e5815631bf`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `psql`, Docker e navegador ausentes.
- Fonte canônica confirmada: leitura/escrita contratual em `tenant_modulo_contratado`; `tenant_modulo` permanece projeção legada unidirecional; catálogo comercial contratável em `modulo_saas`; histórico em `tenant_modulo_contratado_historico`; autorização em identidades/vínculos/perfis/permissões persistidos e no avaliador único.
- Correção: contratação repetida deixa de sobrescrever contrato/histórico; suspensão exige estado elegível; reativação exige estado `SUSPENSO`, tenant ativo, dependências, vigência ainda elegível e cancelamento não efetivado. Suspender/reativar preserva a vigência original; reativar não renova contrato nem quita cobrança.
- Interface: confirmações mostram cliente, módulo, momento e consequências; a revisão esclarece que dependências não são incluídas silenciosamente e contratação não concede permissão.
- Sem migration: o schema canônico existente foi reutilizado. Sem alteração do avaliador, troca de contexto ou dashboard.
- **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16, concorrência real, dois tenants e screenshots nas seis larguras não foram executados por ausência das ferramentas.
- Riscos restantes: snapshot de preço/periodicidade e fluxo cliente solicitar → SuperAdmin aprovar ainda precisam ser unificados com a solicitação comercial canônica; cancelamento contratual não foi acrescentado sem decisão formal de política.
- Próximo item exato: integrar `saas_solicitacao_cliente` ao detalhe canônico para aprovação idempotente que grave preço/periodicidade aceitos em `tenant_modulo_contratado`, sem ativação direta pelo administrador do cliente.

---

Data: 2026-09-15. Execução de OS — progresso verificável e histórico. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

| Jornada solicitada | Classificação comprovada | Evidência e limite |
|---|---|---|
| Login e Minha Central | implementada sem validação | rotas e serviços canônicos preservados; runtime indisponível |
| Criar, consultar e atribuir OS | parcial | API/Dapper existentes; seletores autorizados e prova PostgreSQL pendentes |
| Executar OS atribuída | parcial | técnico vinculado ao usuário é conferido no `UPDATE`; estado e versão são condições persistidas |
| Salvar checklist da execução | parcial | vazio permanece pendente, domínio fechado é validado no servidor e concorrência usa `version`; modelos versionados ainda ausentes |
| Histórico da OS | implementada sem validação | nova leitura tenant-scoped de `os_status_historico`, API autorizada e linha do tempo na execução |
| Inspeções e revisão | ausente | não foi identificada implementação canônica completa de manutenção; não foi criado atalho paralelo |
| Não conformidades e ações corretivas | ausente | estruturas de Qualidade não comprovam esta jornada ligada à OS; permanece backlog |
| Materiais e custos | parcial | serviço OS usa saldo canônico e transação; homologação concorrente PostgreSQL continua pendente |
| GED | por último | nenhuma funcionalidade GED foi criada ou antecipada |

- Contrato aplicado ao checklist: usuário autenticado do tenant deve corresponder ao `usuario_id` do técnico atribuído; a OS deve estar em `AGENDADA`, `EM_DESLOCAMENTO`, `EM_EXECUCAO` ou `PAUSADA`; item, OS e versão precisam coincidir. Resposta vazia salva rascunho pendente, resposta de domínio fechado inválida falha, gravação incrementa a versão e repetição com versão antiga é recusada.
- Contrato aplicado à conclusão já existente e preservado: somente a transição permitida pelo estado é aceita; checklist obrigatório bloqueante e apontamento aberto impedem conclusão; atualização e histórico compartilham transação e conflito de versão faz rollback.
- Interface: detalhe ganhou breadcrumb, orientação “Como usar”, progresso real, estado explícito de salvamento e histórico cronológico. Conteúdo persistido é codificado antes de inserção no HTML.
- Preflight: branch `work`, HEAD inicial `bf0911449f983c81be677241c478c4fbba1704de`, árvore limpa e nenhum remoto/upstream configurado. SDK .NET 10 e PostgreSQL/psql ausentes; Node 20 e Git disponíveis.
- **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16 vazio/upgrade/reexecução, dois tenants, concorrência real e capturas nas seis larguras não puderam ser executados sem runtime e navegador. Nenhuma jornada foi declarada homologada.
- Próximo item exato: criar, em migration aditiva, o modelo de checklist versionado canônico e a instância imutável vinculada à OS; depois implementar inspeção/revisão e somente então não conformidade/ação corretiva. GED permanece por último.

---

Data: 2026-09-15. Evolução vertical de Patrimônio — cadeia de custódia. Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

| Funcionalidade | Estado real | Evidência | Lacuna | Ação desta execução |
|---|---|---|---|---|
| Template/navegação | Compartilhado, não homologado | `_Layout`, `_Sidebar` e componentes existentes | navegador ausente; catálogo nominal patrimonial incompleto | detalhe ganhou breadcrumb, hierarquia, estado vazio, foco e ação protegida |
| Educação — matrícula/frequência | Parcial | serviços Dapper e testes estáticos existentes | transferência e correção auditada ainda pendentes | preservada, sem promoção |
| Patrimônio — movimentação | Parcial, agora com cadeia consultável | serviço transacional, API/Web e histórico real | solicitação/recebimento e seletores nominais ainda pendentes | consulta tenant-scoped, idempotência por operação e origem serializada |
| Patrimônio — inventário | Parcial | abertura, conferência e fechamento persistidos | fotografia temporal e movimentos durante conferência pendentes | preservado, sem promoção |
| Compras/Almoxarifado/Indústria/SaaS | Regressões runtime aguardam Gate A | matrizes e testes existentes | SDK, PostgreSQL e navegador ausentes | nenhuma implementação paralela criada |

- Preflight: branch `work`, HEAD inicial `e88132d578ea90e6964f64efe0a29354391f4fdd`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `psql`, PostgreSQL, PowerShell e navegador ausentes; Node 20 disponível.
- Patrimônio: o detalhe passa a retornar e exibir o histórico persistido por tenant. A movimentação bloqueia destino vazio/inalterado, serializa a origem com `FOR UPDATE`, aceita chave idempotente estável (`Idempotency-Key` na API e campo por formulário), recusa reutilização com conteúdo divergente e conserva atualização, evento e auditoria na mesma transação.
- Interface: status inclui texto, histórico tem estado vazio e tabela responsiva, formulário tem labels, orientação, confirmação, “Como usar”, foco visível e bloqueio contra duplo envio. Não houve alteração de schema.
- Validação estática: JavaScript, JSON e `git diff --check` passaram. **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16, regressões integradas e capturas reais nas seis larguras, pois as ferramentas não estão instaladas. Nenhum módulo foi promovido.
- Riscos restantes: IDs de unidade/responsável ainda são entrada técnica e precisam de catálogo autorizado; fluxo pendente confirmar/receber e concorrência por versão exigem migration própria; inventário ainda não preserva fotografia temporal.
- Próximo item exato: criar o fluxo patrimonial persistido SOLICITADO → RECEBIDO/CANCELADO, com seletores autorizados e versão do bem; depois corrigir fotografia temporal do inventário. GED permanece por último.

---

Data: 2026-09-15. RC51.02Q (integridade do rascunho de compras). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `47b51a7a99888036dc1d3182b33d77121dc28a0c`, árvore limpa, sem remoto/upstream; SDK normativo `10.0.100`. `dotnet`, `pwsh`, `psql`, Docker e navegador permanecem ausentes.
- Regressões: as URLs canônicas `/Governanca/QualidadeDados` e aliases distintos já estavam corrigidos e protegidos por `WebRuntimeSmokeTests`; `PagedResult.TotalItems`, Razor, nullable industrial e Minha Central também permanecem corrigidos no checkout. A requisição com host real ficou BLOCKED pela ausência do runtime.
- Compras: validação de rascunho passou a ser equivalente no cliente e no servidor para justificativa, urgência, tipo, descrição, unidade, quantidade, valor, limite e duplicidade de itens. A criação conserva uma única chave de idempotência durante retry/timeout. O editor ganhou labels visíveis por item e agrupamento semântico responsivo.
- Validação estática: JavaScript, JSON, shell e `git diff --check` passaram. **BLOCKED separadamente:** restore/build/Razor/testes/Swagger, PostgreSQL 16, host Web, screenshots e cenários concorrentes, pois as ferramentas necessárias não estão disponíveis.
- Escopo: rascunho/listagem/detalhe/envio permanecem **PARCIAIS, implementados sem validação runtime**; aprovação configurável, pedido, recebimento/estorno e integração completa com estoque/Indústria permanecem **AUSENTES ou AGUARDA_GATE**. Nenhum módulo foi promovido. GED permanece por último.
- Próximo item exato: executar Gate A com SDK .NET 10 e PostgreSQL 16; depois persistir a política canônica de alçadas e implementar decisão concorrente sem autoaprovação, antes de pedido e recebimento.

---

Data: 2026-09-14. RC51.02O (governança estática do manifesto). Estado: CORRIGIDA ESTATICAMENTE / BLOCKED.

- Preflight: branch `work`, HEAD inicial `b710b05b188edbacee1cb9b30a8e9428fdc00cce`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `pwsh`, `psql`, Docker e navegador ausentes.
- Defeito confirmado: a migration `20260914120000` estava intacta, mas manifesto e consolidados registravam o hash do conteúdo sem a quebra final, divergindo do algoritmo normativo. O checksum foi sincronizado nos sete catálogos e o valor anterior preservado como `knownChecksums` com pós-condição; nenhuma migration publicada foi editada.
- Aplicadores Bash e PowerShell agora recusam metadados históricos/probes malformados antes de DDL. Teste existente ampliado; validações Bash/JSON/diff passaram. O CS8629 industrial já permanece corrigido com pattern matching e HTTP 422 para contexto nulo.
- Sem alteração visual ou schema; screenshot não aplicável. **BLOCKED:** PowerShell ValidateOnly, restore/build/test, OpenAPI, navegador e provas PostgreSQL 16 pela ausência das ferramentas.
- Evidência e matriz: `docs/entregas/RC51-02O-GOVERNANCA-MANIFEST.md`.
- Próximo item exato: executar o ValidateOnly PowerShell e provar no PostgreSQL 16 vazio/reapply/histórico conhecido e desconhecido/upgrade legado; depois validar login e isolamento antes do Gate B SaaS.

---

Data: 2026-09-14. RC51.02N (nullable da integração industrial e clareza operacional). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `84214791f4f4f42ea6ff106225e2932429ae31ad`, árvore limpa, sem remoto/upstream. O SDK normativo é `10.0.100`; `dotnet`, PostgreSQL, Docker e navegador não estão disponíveis.
- Causa do CS8629: após testar `HasValue`, `GerarOsAsync` voltava a ler `_tenant.EntidadeId.Value` e `_user.UsuarioId.Value` dentro de expressões posteriores; a análise nullable não preservava a garantia. `null` significa contexto operacional autenticado incompleto e agora retorna 422 antes de entitlement, consulta ou gravação. Pattern matching captura `entidadeId` e `usuarioId`, usados no avaliador e SQL, sem identidade padrão.
- Regressão: a classe existente `PostBuild06IndustriaTests` protege a validação, a mensagem contratual, o reuso dos valores tipados e a ausência de `.Value`/`?? 0` neste fluxo. Registro ausente/outro tenant, esfera inconsistente, falta de permissão e idempotência continuam protegidos pelos predicados e testes existentes.
- Interface: a página industrial compartilhada explicita a sequência ordem → materiais/reserva → apontamento → qualidade → encerramento, inclui “Como usar” e não converte erro de consulta em lista vazia. A matriz curta e as transições foram registradas em `docs/industria-producao.md`; o módulo permanece `PARCIAL`.
- Sem alteração de schema ou migration. **BLOCKED:** restore/build/test, OpenAPI, login/Minha Central, jornada no navegador, screenshots e provas PostgreSQL 16 não foram executados pela ausência das ferramentas/runtime.
- Próximo item exato: implementar a unidade transacional e idempotência por conteúdo entre reserva/consumo/produção da OP e estoque canônico, seguida da reconciliação concorrente de qualidade, entrada e custo no encerramento; provar em PostgreSQL 16 duas reservas, retries e encerramento concorrente com apontamento. GED permanece por último.

---

Data: 2026-09-14. RC51.02M (aplicador Bash fail-closed). Estado: CORREÇÃO ESTÁTICA / BLOCKED.

- Preflight: branch `work`, HEAD inicial `158cef1116d2b9cc9be28757ec5ea8fc90d5fd9c`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `pwsh`, `psql`, Docker e navegador ausentes. **Erro específico não fornecido** na tarefa.
- Matriz curta: Fundação | havia wrapper Bash que validava apenas migrations e executava todos os SQLs sem ledger/pós-condições | reaplicação e upgrade podiam alterar o banco parcialmente | normalização/checksum de migrations e compatibilidades, seguida de bloqueio explícito da execução insegura | `VALIDATE_ONLY=true`, cenários inválidos sintéticos, `bash -n` e contrato na classe de regressão existente. API/Web, Minha Central, Educação, Saúde/ACS, Jurídico e GED | preservados | Gate A runtime continua ausente | nenhuma evolução funcional posterior iniciada | restore/build/runtime/PG16 permanecem BLOCKED.
- Causa confirmada por inspeção do primeiro executor afetado: `apply-migrations-manifest.sh` ignorava `compatibilityBefore`, `compatibilityAfterAll`, ledger, `knownChecksums` e pós-condições; ainda calculava hash com normalização diferente do runner canônico. A execução DDL foi removida do wrapper até que alcance paridade integral. O modo estático agora aceita BOM UTF-8, normaliza CRLF/CR, valida existência, confinamento e checksum das compatibilidades e recusa duplicidade por migration.
- Sem alteração de schema, migration, manifesto ou scripts consolidados. Nenhuma jornada posterior foi promovida, e não há screenshot porque não houve mudança visual e o navegador/runtime estão indisponíveis.
- Próximo item exato: executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; em PostgreSQL 16, provar banco vazio/reaplicação, rejeição de versão/checksum desconhecidos, `knownChecksums` com pós-condição e upgrade legado formal. Implementar execução Bash somente com o mesmo ledger, atomicidade e pós-condições do runner canônico.

---

Data: 2026-09-14. RC51.02L (integridade mínima de matrícula e frequência). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `dd8b2e08f7252d89b8c9c6ff5083d5de791b4fa3`, árvore limpa, sem remoto/upstream; projetos e migrations confirmados. SDK normativo `10.0.100`; `dotnet`, `psql`, Docker e navegador ausentes. O download do SDK foi tentado e bloqueado pelo proxy HTTP 403.
- Lacuna confirmada: a matrícula era inserida antes da tentativa de incrementar a ocupação e o resultado do `UPDATE` era ignorado. Assim, a disputa pela última vaga podia confirmar duas matrículas e divergira de `vagas_ocupadas`. Identificadores de aluno/escola/ano/turma também não eram validados conjuntamente no contexto. Frequência aceitava aluno sem matrícula elegível e data fora da vigência.
- Implementado: a mesma transação agora reserva exatamente uma vaga antes do insert, sob `UPDATE` concorrente, validando tenant, entidade, cadastros ativos, compatibilidade escola/ano/turma e período não encerrado. Zero linhas atualizadas aborta e faz rollback. O lançamento de frequência passou a ser `INSERT ... SELECT` condicionado a matrícula ativa/confirmada e data entre ingresso e fim do ano letivo; situação e justificativa são validadas no serviço. Ausência de entidade não usa mais o contexto fictício `1`.
- Matriz: Educação permanece **PARCIAL / IMPLEMENTADA SEM VALIDAÇÃO RUNTIME**; Saúde/ACS e Jurídico foram apenas inspecionados e permanecem **PARCIAIS**, sem alteração nesta fatia. SaaS, Compras, Estoque, Indústria, Frota, Contratos e GED foram preservados; GED continua por último.
- Testes estáticos existentes foram ampliados para proteger a ordem reserva→insert e os predicados de contexto/elegibilidade. `git diff --check` passou. **BLOCKED:** restore/build/test, PostgreSQL 16 vazio/upgrade/reexecução, OpenAPI, login/Minha Central/logout, jornadas HTTP e screenshots responsivos não foram executados por ausência de ferramentas/runtime.
- Riscos restantes: os índices históricos de matrícula ainda impõem unicidade ampla por aluno/ano e precisam de migration corretiva sincronizada para representar oferta; transferência ainda só altera o estado da matrícula, sem criar novo vínculo preservando o anterior; não existe operação específica de correção auditada da chamada. Esses pontos impedem o aceite vertical completo.
- Próximo item exato: implementar transferência atômica com histórico e nova matrícula por oferta, acrescentar migration forward-only que substitua a unicidade genérica e sincronizar manifesto/consolidados; aceite em PostgreSQL 16: uma vencedora na última vaga, oferta complementar legítima, transferência preservando frequência anterior e retry sem duplicação.

---

Data: 2026-09-14. RC51.02K (correção Razor e paginação de uso SaaS). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Projeto confirmado: `src/Sigov.Web/Sigov.Web.csproj`; branch `work`, HEAD inicial `e23e8c18a1af8e5c07386fb5cb019c6c4a47a87b`, árvore inicialmente limpa. O SDK normativo permanece .NET `10.0.100` e nenhuma alteração foi feita no Agro360.
- Causa Razor confirmada: `Uso.cshtml` é uma View MVC e declarava a variável local `page`; a expressão implícita `@page` no texto da paginação era interpretada pelo parser como a diretiva Razor reservada. Não existia motivo para adicionar a diretiva `@page` nem converter a tela em Razor Page. A variável passou a `pageNumber` e a saída textual usa expressão explícita.
- Evolução verificável: paginação de `auditoria_evento` passou ao PostgreSQL, com `limit/offset`, total independente da página e ordenação estável por instante e identidade; páginas fora do intervalo retornam à última válida. Cliente, módulo, situação e período são preservados nos links e na exportação; cliente agora é escolhido pelo catálogo retornado pelo servidor, não por digitação de ID.
- Sem números fictícios: total e última atividade vêm do histórico persistido; usuário ativo continua explicitamente indisponível porque o schema atual não tem evento canônico de uso por identidade. A interface explica período UTC, significado da métrica, limitações de retentativa e diferença entre indisponibilidade e ausência de registros.
- Autorização existente preservada: SuperAdmin continua avaliado pelo serviço canônico e administrador local é restringido ao próprio `tenant_id`; a exportação reutiliza filtros e avaliação do endpoint. Nenhuma migration ou mudança estrutural foi necessária.
- Validação estática: busca contextual das demais Views encontrou `page` apenas dentro do JavaScript de Indústria, sem colisão Razor; balanceamento dos elementos principais e `git diff --check` passaram. **BLOCKED:** `dotnet restore`, `dotnet build`, `dotnet test`, renderização HTTP, PostgreSQL 16 e screenshot não foram executados porque `dotnet`/runtime/banco/navegador não estão disponíveis neste ambiente.
- Próximo item exato: instalar o SDK `10.0.100`, executar restore/build/test e validar `/SaasAdmin/Uso` com PostgreSQL 16 para tenant global, administrador local, filtros combinados, página excedente e exportação; depois instrumentar atividade por usuário somente quando houver evento canônico idempotente.

---

Data: 2026-09-14. RC51.02J (SaaS Admin — uso verificável). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `8f07ab1c54d79d85237f4fdb11725d73010a09b2`, árvore limpa, sem remoto ou upstream configurado.
- Problema | Causa | Serviço reutilizado | Correção | Evidência: Uso exibia números e um município demonstrativos | `Uso.cshtml` era uma cópia estática e `saas.uso.js` não implementava consulta | `ISuperAdminOperationalDashboardService`/`auditoria_evento` | filtros de cliente/período/módulo, paginação, exportação existente e indisponibilidade explícita | controller e view recebem `SuperAdminOperationalDashboard`; script impede duplo envio.
- Problema | Causa | Serviço reutilizado | Correção | Evidência: script de clientes enviava `X-Sigov-Tenant: municipio-demo` | contexto fixo no navegador | avaliador de autorização e contexto já aplicados pelo controller | cabeçalho removido; servidor permanece autoridade | `saas.tenants.js` não resolve nem escolhe tenant.
- Métrica entregue: “operações concluídas” conta os eventos persistidos em `auditoria_evento`, no intervalo UTC inclusivo consultado; não soma login, visualização, decisão de autorização ou falha. Não há deduplicação adicional na leitura: retries só não inflam o resultado quando a operação canônica não cria novo evento. Usuários ativos, limites e uso por módulo permanecem “medição indisponível” enquanto não houver evento canônico verificável.
- Preservado: consultas Dapper parametrizadas, autorização global/local existente, isolamento pelo `tenantId` autorizado, catálogo e contratos canônicos e exportação protegida. Sem alteração de schema ou migration.
- Validação estática: `node --check` nos dois scripts, conflito de rotas API e `git diff --check` passaram. **BLOCKED:** restore/build/test, PostgreSQL 16, runtime autenticado e screenshots responsivos, pois SDK, banco e navegador não estão disponíveis neste ambiente.
- Próximo item exato: instrumentar uma chave idempotente canônica na conclusão de uma operação já auditada e provar, em PostgreSQL 16, que retry mantém um evento; aceite: filtro de dois clientes não mistura eventos e exportação contém exatamente a mesma janela autorizada da tela.

Data: 2026-09-11. RC51.02I (onboarding contextual persistido). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `75bdee0d096d6c948f4fea9e21757eb7f6c20420`, árvore limpa e sem remoto/upstream.
- Removidos os 12 passos e três conclusões fabricadas; Application agora agrega jornada/etapas/tarefas obtidas pelo repositório Dapper existente.
- API e Web exigem autenticação/contexto e recusam tenant divergente; ausência de jornada retorna 404.
- Sem migration: reutilizado schema publicado. Detalhes em `docs/entregas/RC51-02I-ONBOARDING-CONTEXTO-PERSISTIDO.md`.
- BLOCKED: .NET, PowerShell, PostgreSQL 16, Docker e navegador ausentes; download do SDK bloqueado por HTTP 403.
- Próximo item: executar gates e isolamento runtime do onboarding em dois tenants; depois concluir convites.

---

Data: 2026-09-11. RC51.02H (central e navegação contextual). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: raiz `/workspace/SIGOV-PLUS`; branch `work`; HEAD inicial `ac06642902bab7b52373cbe4473432a349cf8967`; árvore limpa; sem remoto/upstream.
- Corrigidos totais/vencimentos reais, exercício selecionado, estados 403/503/vazio e removido o indicador GED sem fonte operacional.
- Favoritos e recentes foram isolados por identidade/contexto, com URL interna, descarte legado, revalidação de visibilidade e `textContent`.
- Paleta não simula disponibilidade com catálogo local; navegação ativa compara segmentos e aplica `aria-current`.
- `node --check` dos três scripts e `git diff --check`: PASS. Gates .NET/PostgreSQL, runtime e screenshots: BLOCKED por ausência das ferramentas.
- Evidência e matriz: `docs/entregas/RC51-02H-CENTRAL-NAVEGACAO.md`.

Data: 2026-09-11. RC51.02G (central de trabalho fail-closed). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`; HEAD inicial `d74a9d238156214f1f2301e562bd437565cc733a`; árvore limpa; checkout sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `pwsh`, `psql`, PostgreSQL 16 e navegador não estão disponíveis neste ambiente.
- Existente: `MinhaCentralService`, `pendencia_operacional`, shell autenticado e componentes visuais compartilhados. Lacuna confirmada: a central consultava todas as pendências abertas do tenant, sem limitar ao usuário responsável, e convertia contexto/schema/banco indisponível em painel vazio HTTP 200.
- Complementado: tenant e usuário agora são contexto obrigatório; o tenant precisa existir e estar ativo; a worklist usa query parametrizada por `tenant_id` e `responsavel_usuario_id`; ausência da estrutura ou falha de consulta é propagada e apresentada como indisponibilidade HTTP 503, com referência de correlação e sem pendências simuladas.
- Serviço canônico preservado: `MinhaCentralService` continua apenas agregando `pendencia_operacional` e encaminhando ao documento original; nenhum segundo workflow foi criado. Dependências: autenticação persistida, tenant ativo e migration `20260819150000`. Aceite desta fatia: usuário não recebe tarefa atribuída a outro usuário/tenant e indisponibilidade não aparenta sucesso.
- Validação estática: `git diff --check`, conflito de rotas, JSON do manifesto, artefatos rastreados e marcadores de conflito passaram. **BLOCKED separadamente:** restore/build/test, PostgreSQL 16 e screenshot real, por ausência das ferramentas/runtime.
- Próximo item exato: executar restore/build/test e validar no PostgreSQL 16 duas sessões do mesmo tenant com pendências atribuídas distintas; em seguida implementar a fila de aprovações disponíveis por autorização persistida, sem tornar pendência sem responsável visível por padrão.

---

Data: 2026-09-10. RC51.02F (diagnóstico independente da correção de fundação). Estado: PARCIAL / BLOCKED.

- Preflight: branch `work`; HEAD inicial `01d7f64f893a86b3c3cacf50f907aabe09bb487d`; árvore limpa; sem remoto e sem upstream configurados. SDK normativo `10.0.100`; projetos Domain, Application, Infrastructure, Api, Web, Worker e quatro projetos de teste confirmados.
- Matriz curta: Fundação — existente/PARCIAL: runner e aplicador com ledger/probes; lacuna: runtime PG16; serviço canônico: `MigrationRunner`; dependência: dotnet/pwsh/psql; aceite: vazio, upgrade e reaplicação. Visual/SaaS — existente/PARCIAL: componentes compartilhados, catálogo persistido e entitlements; lacuna: Gate A e runtime; serviços canônicos: avaliador/catálogo SaaS; dependência: Fundação; aceite: administração isolada por tenant. Indústria, Almoxarifado, Contratos, Frotas/Patrimônio, Educação/Saúde/Jurídico — existentes/PARCIAIS ou ESTRUTURA; lacuna: jornadas integrais e prova runtime; dependência: Gates A/B; aceite: invariantes específicos sem duplicar livros/autoridade. GED — PARCIAL e mantido por último.
- Lacuna corrigida: a migration corretiva `20260910120000` tinha uma pós-condição booleana agregada, que não identificava isoladamente permissão ausente/inativa, tipo/nulabilidade de `perfil_acesso.tenant_id`, tabela histórica ou função/trigger SaaS divergente. O manifesto agora contém cinco probes nomeados, com diagnóstico esperado/obtido, e mantém a pós-condição final forte.
- Preservado: migrations SQL publicadas e consolidados não foram alterados; não houve mudança de schema. O teste de regressão existente passou a exigir os cinco invariantes e a força da pós-condição final.
- Validação estática: JSON e `git diff --check` passaram. **BLOCKED separadamente:** restore/build/test (.NET ausente), parser PowerShell (`pwsh` ausente), PostgreSQL 16 limpo/upgrade/reexecução (`psql` ausente), Swagger/login/MinhaCentral/logout/isolamento e screenshots reais (runtime e navegador ausentes).
- Próximo item exato: executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; depois aplicar em PostgreSQL 16 vazio, reaplicar e executar upgrade legado autorizado, exigindo todos os cinco probes aprovados e `pendentes=0; checksum=0; falhas=0`. Somente então liberar Gate B SaaS.

---

Data: 2026-09-10. RC51.02E (ledger seguro no aplicador operacional). Estado: PARCIAL / BLOCKED.

- Branch observada: `work`; HEAD inicial `cd0f4c4e43ed0cdcbf4e57fd6ff6b3bc2039c471`; árvore inicialmente limpa; sem remoto e sem upstream configurados.
- Comportamento preservado: manifesto com 176 entradas/172 automáticas e probes determinísticos das três pós-condições; `MigrationRunner` .NET já recusava versão/checksum desconhecido.
- Lacuna confirmada: `apply-migrations-manifest.ps1` silenciava falha de leitura do ledger, não recusava versão desconhecida, recusava checksums históricos declarados e exigia `psql` até em `-ValidateOnly`. Também reaplicava compatibilidades finais quando nenhuma migration fora aplicada.
- Implementação: o aplicador agora distingue banco limpo de falha de consulta, valida toda versão do ledger contra o manifesto, aceita apenas checksum atual ou `knownChecksums` com pós-condição forte, revalida entradas históricas presentes, não executa `compatibilityAfterAll` numa segunda passagem sem pendências e permite validação estática sem cliente PostgreSQL.
- Critério/evidência: JSON, shell e `git diff --check` passaram; contratos de regressão foram ampliados na classe existente. **BLOCKED:** parser PowerShell, build/testes .NET e runtime PostgreSQL 16 não executados porque `pwsh`, `dotnet` e `psql` não existem no ambiente.
- Dependências e próximo item: executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; depois testar banco vazio, segunda passagem, ledger com versão desconhecida, checksum histórico conhecido/desconhecido e upgrade legado autorizado em PostgreSQL 16. Gate B continua bloqueado.

---

Data: 2026-09-10. RC51.02D (validação determinística das pós-condições). Estado: PARCIAL / BLOCKED.

- Branch observada: `work`; HEAD inicial `86a8cfcb3803221735426bf937924606c1c201ba`; working tree inicialmente limpa; branch sem upstream e checkout sem remoto configurado, portanto a comparação atualizada com `origin/main` ficou BLOCKED sem alterar o trabalho local.
- As correções forward-only e os probes separados das migrations `20260802210000`, `20260819120000` e `20260908120000` já existiam no HEAD inicial. A lacuna encontrada estava no aplicador PowerShell: ele executava apenas `postConditionSql`, ignorava `postConditionProbes` e, na reaplicação, pulava toda validação das versões presentes no ledger.
- Implementado: probes nomeados agora participam da mesma transação antes do registro no ledger. Ao final, o aplicador revalida a pós-condição booleana e cada probe de todas as migrations automáticas, inclusive quando o ledger já estava completo. Falhas preservam o nome da versão/invariante e não são convertidas em sucesso.
- Compras permanece com o rename conservador do bootstrap 070, contrato empresarial UUID e contrato governamental bigint separados; Educação/Saúde preserva template global anulável sem conceder perfil; SaaS preserva nome/disponibilidade comerciais administráveis e valida tabela, tipos, constraint, funções e triggers separadamente.
- **BLOCKED:** restore/build/test e parser PowerShell não executados porque `dotnet` e `pwsh` não existem no ambiente desta execução.
- **BLOCKED:** PostgreSQL 16 vazio, reaplicação, upgrade sanitizado, equivalência, Swagger, login/MinhaCentral/logout, isolamento e validação visual não executados porque `psql`, runtime e navegador não estão disponíveis/configurados.
- Próximo comando exato: `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; em seguida, com PostgreSQL 16 descartável configurado, executar aplicação vazia, reaplicação e upgrade sanitizado antes de liberar Gate B.

---

Data: 2026-09-10. RC51.02C (recalc + remoto). Estado: PARCIAL / BLOCKED. Gate A quase fechado em runtime Podman PG16; Gate B–D não iniciados.

## Preflight recalculado

- Branch atual: `codex/rc51-02c-foundation-saas-multimodulo` (criada de `c6e74776`).
- Branch anterior / PR #385: `codex/rc51-02c-foundation-saas-industria-evolucao` @ `c6e74776` (open, mergeable_state=unstable, CI status pending/vazio).
- `origin/main`: `dc7c1ac23f0c4ee95d7362e149157f2bec8284dc` (inalterado após fetch --prune).
- Remoto: `https://github.com/devmnsoft/SIGOV-PLUS.git`.
- Working tree: limpa após commits da RC; alterações locais de runtime (Podman/DB) não versionadas.
- Layout: `src/Sigov.{Api,Web,Application,Infrastructure,Domain,Worker}` + `tests/Sigov.{UnitTests,IntegrationTests,ApiTests,Testing}` + `database/postgres/migrations` confirmados.

## Inventário migrations (recalculado)

| Métrica | Valor |
|---|---|
| SQL em `database/postgres/migrations` | 185 |
| Entradas no manifesto | 175 |
| `applyAutomatically=true` | 171 |
| `includeInBaseline=true` | 171 |
| Excluídas de apply | 4 |
| Órfãs (SQL fora do manifesto) | 10 |
| Versões duplicadas no manifesto | 0 |
| Arquivos duplicados no manifesto | 0 |
| SHA-256 do manifesto | `10eda0576ee35a3db1d1c31d6fb2465a3ddb671c72fbda4f7bc8fc11f94a6f93` |
| Maior versão declarada | `20260909120000` |

Órfãs preservadas (não aplicadas automaticamente). Duas compartilham o prefixo de versão `20260813120000` **fora do manifesto**:
`20260813120000_rc50_24_educacao_rh_folha_produto_core.sql` e `20260813120000_rc50_27_operacoes_inteligentes.sql`.

## Gate A — evidência reconfirmada

- Runtime: Podman `postgres:16-alpine` `sigov-pg16-gatea` em `127.0.0.1:5433` (shm 1 GiB). Docker Desktop continua instável neste host.
- **PASS:** `bash scripts/check-tracked-artifacts.sh`
- **PASS:** senhas literais em appsettings versionados = 0 (`Password: ""` apenas)
- **PASS:** apply vazio = 171; reaplicação idempotente = 171\|171
- **PASS:** `script_completop.sql` one-shot em `sigov_gate_a_oneshot` = EXIT 0 (engine permaneceu Up)
- **PASS:** equivalência semântica colunas `information_schema` migração vs one-shot = **EQUIVALENT** (41 172 linhas cada)
- **PASS:** Swagger HTTP 200 (3 445 457 bytes) reconfirmado
- **PASS:** build Release `-warnaserror` (`sigov.runtime.slnf` e `sigov.sln`)
- **PASS:** testes 389 Unit + 123 Integration + 102 Api
- **PASS:** rotas API 630 sem conflito direto; `validate-rc50-80.py` PASS; governance static PASS
- **PASS auth:** login/login-email/CPF/CNPJ → MinhaCentral 200; logout POST → login; dois tenants (`admin`=SIGOV Local id5 vs `admin_t1`=Tenant de Desenvolvimento id1) com hero distinto (`ISOLATION_OK=True`)
- **BLOCKED:** upgrade legado formal (banco com ledger antigo → apply residual) não executado nesta recalculação
- **BLOCKED/parcial:** troca de senha, revogação de sessão, tenant suspenso e acesso cruzado API profundo não reexecutados ponta a ponta nesta passagem
- Claims `HasClaim("permission"|"permissao")` em src = 0; `RequestPermissionClaimsTransformation` permanece no-op
- Remanescente Gate B: dois `IModuleCatalogService` (Commercial sync hardcoded em Program + SaaS async `PersistentModuleCatalogService` no DI)

## Matriz de causas-raiz (estado atual)

| Problema | Causa raiz | Arquivo responsável | Teste reprodutor | Correção |
|---|---|---|---|---|
| Equivalência one-shot derrubava Docker | Engine Desktop/VHD instável + shm baixo | Docker Desktop / host | `psql -f script_completop.sql` | Podman WSL + `--shm-size=1g` → PASS |
| MinhaCentral 500 pós-login | Ícone `workflow` ausente + size 34 + Dapper record | `IconRegistry`, `SigovIconTagHelper`, `AtividadeRecenteViewModel` | Login → `/MinhaCentral` | Registrar ícones/sizes; VM com props → PASS |
| Claims pontuais | Controllers ainda usavam `HasClaim` | `MatrizAcessoController`, `CommercialControllers` | Grep `HasClaim("permission")` | Snapshot/serviço → 0 matches |
| Baseline incompleto | `enterprise_tenant_mapping` auto sem baseline | `manifest.json` | compare schema | `includeInBaseline=true` + scripts → 171 |
| Dois tenants sem evidência | Seed só no tenant 5 | seeds/runtime | Login `admin_t1` | Seed entidade/exercício/usuário tenant 1 → PASS hero |
| Dual catálogo | Interfaces homônimas em namespaces distintos | Commercial vs Saas.Modules | DI/Program | Pendente Gate B |
| Upgrade legado | Não executado | n/a | apply residual em ledger antigo | BLOCKED |

## Divergências vs auditoria `ec799b75` / docs anteriores

1. **Baseline 170 → 171:** docs antigos e auditoria falavam 170; após incluir `enterprise_tenant_mapping` no baseline, o valor recalculado é 171.
2. **Login HTTP:** estava BLOCKED (PG caiu); agora PASS (e-mail/login/CPF/CNPJ + MinhaCentral + logout).
3. **Equivalência:** estava BLOCKED por crash Docker; agora PASS em Podman (41 172 colunas equivalentes).
4. **Dois tenants:** estava BLOCKED; agora PASS no shell MinhaCentral (hero por tenant). Isolamento de dados/API profundo ainda parcial.
5. **Branch:** contrato pediu `codex/rc51-02c-foundation-saas-multimodulo`; PR #385 usava `...industria-evolucao`. Ambas apontam ao mesmo SHA `c6e74776` até o commit documental desta recalculação.
6. **BACKLOG item 1 (“184 arquivos”):** inventário atual = **185** SQLs.
7. **`validate-rc50-80` “4 baselines”:** refere-se ao conjunto de baselines/exclusões do validador, não a `includeInBaseline=171`.
8. **Gates 2–5 / B–D:** continuam **não iniciados** (Gate A ainda incompleto pelo upgrade legado formal).

## Não iniciado

Gate B SaaS canônico; Gate C OP industrial; Gate D Compras/Almoxarifado–Jurídico–Educação; backlog detalhado dos demais; GED por último.

## Próximo item exato

Executar **upgrade legado formal** em PostgreSQL 16 (ledger antigo → apply residual com `pendentes=0 checksum=0 falhas=0`) e fechar evidências de revogação/tenant suspenso/acesso cruzado API; só então liberar Gate B.

---

Data: 2026-09-10. RC51.02A (continuação). Estado: PARCIAL / BLOCKED. Gate A parcialmente evidenciado em runtime; Gate A não aprovado; Gate B e Gate C não iniciados.

- Branch: `codex/rc51-02a-foundation-saas-industria-core` sobre `origin/main` `2646b374` (merge do PR #383).
- Runtime PostgreSQL 16.15 descartável em `127.0.0.1:5433` (container `sigov-pg16-gatea`).
- **PASS:** apply vazio = 171 migrations automáticas registradas; segunda aplicação idempotente (skip de ledger) = PASS.
- **PASS:** Swagger HTTP 200 em API local (`/swagger/v1/swagger.json`, ~3,4 MiB) com `MigrationMode=Disabled` apontando ao banco Gate A.
- **PASS estático de senha:** `check-local-login.ps1` validou hash de `admin` e `superadmin`.
- Correções de suporte ao Gate A: `apply-migrations-manifest.ps1` (StrictMode, pós-condição via arquivo, skip de já aplicadas); manifesto alinhou pós-condições de `20260802210000` e `20260826130000` ao SQL publicado; `AuthenticationRepository` reordenou colunas para materialização Dapper de `AuthenticationUser`.
- **BLOCKED:** equivalência schema vs `script_completop.sql` — Docker Desktop derrubou o engine durante o apply/compare.
- **BLOCKED:** login HTTP ponta a ponta / MinhaCentral / logout / dois tenants — banco 16 caiu (connection refused) durante as tentativas; cookie autenticado não foi obtido.
- **BLOCKED:** upgrade legado formal e isolamento de dois tenants.
- Gate B e Gate C não avançaram (Gate A runtime incompleto).
- Próximo item: estabilizar PostgreSQL 16 descartável (sem colisão com PG18 na 5432), fechar equivalência/legado, login HTTP e-mail/CPF/CNPJ + isolamento; só então Gate B e Gate C.

---

Data: 2026-09-10. RC51.02A. Estado: PARCIAL / BLOCKED. Gate A estático reparado; Gate A runtime, Gate B e Gate C não aprovados.

- Branch: `codex/rc51-02a-foundation-saas-industria-core`, criada de `origin/main` `8b6e867b2e76638d5d809ca6a3462b1d7d60756c`.
- `origin/main` avançou após a auditoria `ec799b75`: merge do PR #381 e do PR #382 (RC51.02), depois `d45dc8e ajuste`, `377b178 sasa` e `8b6e867 aDASDAs`.
- Causa raiz da regressão: esses três commits recolocaram 14 arquivos de `obj` no índice Git e a senha literal no `src/Sigov.Worker/appsettings.json`.
- Correção Gate A (estática): `git rm --cached` dos 14 artefatos (arquivos físicos preservados); `.gitignore` e o gate passam a recusar `*.cache.json`; Worker versionado sem connection string.
- Preservado sem commit: alterações locais de `obj` geradas pelo build.
- Inventário: 185 SQLs, 175 entradas, 171 automáticas, 4 excluídas de apply, 170 baseline, 10 órfãs classificadas. `20260902010000`, `20260903130000` e `20260909120000` permanecem declaradas.
- Testes: UnitTests 389 (1 s), IntegrationTests 123 (5 s), ApiTests 102 (9 s). Restore/build Release `-warnaserror` PASS. `check-tracked-artifacts` PASS; `validate-rc50-80.py` PASS; rotas API 630 sem conflito direto; `git diff --check` PASS.
- `BLOCKED: Gate A runtime — PostgreSQL 16 vazio/reaplicação/legado/equivalência, Swagger HTTP 200 e login/isolamento não executados. psql ausente, Docker daemon desktop-linux indisponível, ConnectionStrings__DefaultConnection/PG* ausentes. PostgreSQL 18 local existe somente como diagnóstico e não substitui o gate 16.`
- `BLOCKED: Gate B não avançado — o contrato exige Gate A runtime verde. Catálogo Commercial hardcoded e telas SaaS Admin ainda incompletas em relação ao aceite B2 permanecem pendentes.`
- `BLOCKED: Gate C não iniciado — jornada de Ordem de Produção condicionada a A e B verdes. Telas industriais continuam genéricas (`ModulePage`) e o dashboard ainda usa timer de sucesso.`
- Próximo item exato: disponibilizar PostgreSQL 16 descartável, concluir vazio/reaplicação/legado/equivalência, Swagger 200 e login/dois tenants. Só então fechar Gate B (SaaS Admin completo) e Gate C (OP industrial). GED permanece por último.

---

Data: 2026-09-10. RC51.02. Estado: PARCIAL / BLOCKED; P0 estático e entitlement/SaaS Admin implementados no código; PostgreSQL 16 vazio/reaplicação/legado, Swagger HTTP 200 e login ponta a ponta permanecem BLOCKED neste host.

- Branch: `codex/rc51-02-p0-runtime-entitlement-canonico`, baseada em `origin/main` `ec799b75a6a1b3156566afc8a21216748f82df24` (HEAD inicial da sprint anterior `0675787277addc39df526515d431900f27e4248a` reaproveitada como fundação).
- Causa raiz 1: `check-tracked-artifacts.sh` imprimia PASS fora de worktree porque `git ls-files | rg` falhava dentro de `if`; agora falha se git/rg/worktree/ls-files falharem e só imprime PASS no final.
- Causa raiz 2: `MigrationRunner` resolvia o caminho no construtor e `DatabaseOptions.MigrationsPath` tinha default relativo enganoso. Construtor ficou lazy; `MigrationMode=Disabled` não toca o diretório.
- Causa raiz 3: `RequestPermissionClaimsTransformation` recolocava permissões/módulos no principal; sidebar e `UserPermissionService` decidiam por claims. Snapshot request-scoped (`IRequestAuthorizationSnapshot`) carrega uma vez por request; policies usam `PersistedPermissionHandler`.
- Causa raiz 4: dois `IModuleCatalogService` hardcoded. A interface canônica em Application.Saas.Modules passou a ser assíncrona; DI usa `PersistentModuleCatalogService` lendo `modulo_saas`. `IModuleEntitlementEvaluator` é a decisão única de contrato+dependência+permissão.
- SaaS Admin: SuperAdmin lista tenants (status, esfera, entidades, usuários ativos, módulos, última atividade), abre detalhe e contrata/suspende/reativa com justificativa, transação, concorrência e auditoria. Admin local não altera catálogo/preço e só vê o próprio tenant.
- Indústria: `RequireModule("industria_producao")`, menu e `IndustriaComercialService` usam o avaliador; SQL direto de contratação de `industria_producao` removido. Status permanece PARCIAL.
- Manifesto estático: 185 SQLs, 175 entradas, 10 órfãos classificados preservados; 171 automáticas, 170 baseline; `20260902010000`, `20260903130000` e `20260909120000` permanecem declaradas. SHA do manifesto deve ser relida no runtime.
- Testes: UnitTests 389, IntegrationTests 123, ApiTests 102. `bash scripts/check-tracked-artifacts.sh` PASS; `python -m json.tool database/postgres/migrations/manifest.json` PASS; `python scripts/validate-rc50-80.py` PASS; `bash scripts/check-api-route-conflicts.sh` PASS em 630 rotas; `git diff --check` PASS.
- `BLOCKED: PostgreSQL 16 vazio, segunda passagem idempotente, upgrade legado, equivalência, API ApplyPending/ValidateOnly, Swagger HTTP 200, login e-mail/CPF/CNPJ, MinhaCentral, logout, revogação, tenant suspenso, dois tenants e SaaS Admin ponta a ponta não foram executados porque psql não está no PATH, ConnectionStrings__DefaultConnection/PG* ausentes e o Docker daemon desktop-linux está indisponível.`
- ADR de catálogo/entitlements e de autenticação permanecem sem ACEITO até evidência runtime PostgreSQL 16.
- Próximo item após todos os gates verdes: **RC51.03 — Ordem de Produção industrial integrada: demanda/pedido → BOM versionada → roteiro versionado → disponibilidade e reserva atômica → liberação da OP → apontamento → consumo por lote → qualidade → entrada do acabado → custo real/variação → encerramento e rastreabilidade.**

---

Data: 2026-09-10. RC51.02. Estado: PARCIAL / BLOCKED; runtime estático de migrations estabilizado, artefatos rastreados saneados, P0 PostgreSQL 16 bloqueado e SaaS Admin não iniciado.

- Branch: `codex/rc51-02-migration-runtime-saas-admin`, baseada em `origin/main` `ec799b75a6a1b3156566afc8a21216748f82df24`.
- Estado preexistente antes da sprint: `.vs`, `bin` e `obj` rastreados e modificados; `src/Sigov.Worker/appsettings.json` modificado fora do escopo.
- Causa raiz: `Sigov:Database:MigrationsPath = "database/postgres/migrations"` era resolvido contra `Directory.GetCurrentDirectory()`/ContentRoot em `src/Sigov.Api`, fazendo a API procurar `src/Sigov.Api/database/postgres/migrations`.
- Correção: `MigrationsPath` relativo foi removido do `appsettings.json` local da API; Docker mantém `/app/database/postgres/migrations`; o resolvedor agora considera ContentRoot, CurrentDirectory e BaseDirectory, sobe ancestrais, valida `manifest.json`, recusa `.vs/bin/obj/artifacts/TestResults`, registra origem e falha em ambiguidade.
- Caminho canônico local esperado para Visual Studio: `C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations`.
- Manifesto canônico atual: `C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\manifest.json`.
- Artefatos: 1.084 entradas removidas somente do índice Git; arquivos físicos de trabalho preservados. Diretórios afetados: `.vs`, `src/*/bin`, `src/*/obj`, `tests/*/bin` e `tests/*/obj`.
- Commit de retorno dos artefatos no main atual: `ec799b75` (`dsdsds`), com origem histórica detectada em `f6bb7f24` e `4a466e38` para amostras de artefatos.
- Prevenção: `.gitignore` passou a ignorar `*.dll`; `build-test` no CI agora depende de `tracked-artifacts`, garantindo o gate antes de restore/build/test.
- Testes: `MigrationSqlPolicyTests` ampliado na classe existente para cobrir raiz do repo, Visual Studio em `src/Sigov.Api`, ContentRoot, BaseDirectory em `bin/Debug/net10.0`, absoluto válido/inválido, relativo em ancestral, relativo ausente, múltiplos checkouts, manifesto em `bin`, Docker absoluto, pacote publicado e contrato `MANIFEST_OUTDATED`.
- Evidência local: `bash scripts/check-tracked-artifacts.sh` PASS; `python -m json.tool database/postgres/migrations/manifest.json` PASS; `dotnet clean sigov.sln` PASS; `dotnet restore sigov.sln --locked-mode` PASS; `dotnet build sigov.sln -c Release --no-restore --nologo -warnaserror` PASS; `dotnet test sigov.sln -c Release --no-build` PASS com UnitTests 385, IntegrationTests 123 e ApiTests 100; `bash scripts/check-api-route-conflicts.sh` PASS em 630 rotas; `git diff --check` PASS; `rg -n '^(<<<<<<<|=======|>>>>>>>)' src database tests docs .github` PASS.
- `BLOCKED: PostgreSQL 16 vazio, segunda passagem idempotente, upgrade legado, pós-condições, equivalência semântica, API ApplyPending/ValidateOnly, Swagger HTTP 200, login, MinhaCentral, logout, revogação, tenant suspenso, dois tenants e acesso cruzado não foram executados porque psql não está instalado/no PATH, não há ConnectionStrings__DefaultConnection/PG* no ambiente e o Docker daemon desktop-linux está indisponível.`
- `BLOCKED: SaaS Admin/Entitlements funcional não foi iniciado porque P0 PostgreSQL 16 não foi aprovado.`
- Próximo item exato: disponibilizar PostgreSQL 16 descartável e concluir P0.1-B. Após P0 e entitlement canônico aprovados, seguir para **RC51.03 — Indústria: Ordem de Produção integrada demanda/venda → BOM/ficha técnica → roteiro → reserva de materiais → OP → apontamento → consumo → qualidade → produto acabado → estoque → custos → rastreabilidade**.

---

Data: 2026-09-09. RC51.01. Estado: PARCIAL / BLOCKED; catálogo runtime corrigido, validação PostgreSQL 16 e fase SaaS Admin não liberada.

- Branch: `codex/rc51-01-history-manifest-saas-entitlements`; HEAD inicial `3efdc97520d0db9019182705a645ff2c975ff84b`, sem upstream disponível no ambiente.
- Remoto configurado: `https://github.com/devmnsoft/SIGOV-PLUS.git`; atualização de `origin/main` BLOCKED por proxy HTTP 403. O HEAD inicial é o próprio merge commit obrigatório.
- Causa raiz confirmada: o parser adicionava ao modelo runtime somente entradas `applyAutomatically=true`, e a validação do ledger confundia migrations históricas declaradas com versões desconhecidas.
- Correção: coleções explícitas declaradas, automáticas, excluídas e baseline; histórico validado contra todas as declaradas; DDL restrito às automáticas ausentes; excluídas ausentes reportadas como `Excluded`.
- Diagnóstico do runner agora registra caminho absoluto, SHA-256 e contagens do manifesto, maior versão, diretório corrente e base da aplicação; candidatos ambíguos são recusados.
- Manifesto canônico estático: `/workspace/SIGOV-PLUS/database/postgres/migrations/manifest.json`; SHA-256 `5bcc4eb4f0935ec77d77ca799f4fd12006dd901fa687447957d39e813f5675cd`. O runner registrará ambos na execução; a evidência runtime não pôde ser produzida sem .NET/PostgreSQL.
- `BLOCKED: dotnet restore/build/test, Swagger, login, MinhaCentral, logout e revogação porque o executável dotnet não está instalado.`
- `BLOCKED: PostgreSQL 16 vazio, reaplicação e upgrade com ledger histórico porque psql, Docker e instância descartável não estão disponíveis.`
- Fase SaaS Admin não iniciada, conforme a proibição de iniciar P1 antes de todas as validações P0 passarem.

Próximo item exato: “RC51.02 — Ordem de Produção industrial integrada: demanda/venda → reserva de materiais → OP → apontamento → consumo → qualidade → produto acabado → estoque → custos e rastreabilidade.” GED continua obrigatoriamente por último.

---

Data: 2026-09-09. RC51.00. Estado: PARCIAL / BLOCKED; P0 estático estabilizado, P0 runtime PostgreSQL 16 ainda pendente.

## Execução RC51.00

- Branch: `codex/rc51-00-p0-saas-industria-integrada`, baseada em `origin/main` `6159822b17e31950e4664eed898b2ddc62bde5d2`.
- Saneamento Git: artefatos `.vs`, `bin`, `obj`, `artifacts`, TRX, PDB, caches e backups locais removidos somente do índice; arquivos físicos preservados.
- CI: gate `tracked-artifacts` adicionado para impedir retorno de artefatos gerados rastreados.
- README consolidado para .NET 10/C# 14, PostgreSQL 16+, Dapper, execução local primária sem Docker, Docker opcional e status conservador dos módulos.
- Autenticação API: `SigovApiAuthenticationHandler` valida API key de `/api/v1` por hash em `sigov.api_key`, escopos em `sigov.api_key_escopo` e bearer de sessão persistente; middleware `ApiKeyV1Middleware` ficou responsável por tenant, escopo e auditoria.
- Sessão Web: login cria sessão persistente em `sigov.identidade_sessao`, cookie carrega referência mínima, validação ocorre por request, logout e troca de senha revogam sessões.
- Migration nova: `20260909120000_rc51_identidade_sessao_persistente.sql`.
- Manifest/scripts: `manifest.json`, `database/postgres/script_completo.sql`, `database/postgres/script_completo_dev.sql`, `database/script_completo.sql`, `script_completo.sql` e `script_completop.sql` sincronizados; baseline com 170 migrations incluídas e 5 excluídas.
- GED: `20260902000000_rc50_98_ged_workflow_branding_logo.sql` preservada como histórica, removida de aplicação automática/baseline porque cria schema físico `ged.*` e GED está fora da RC51.00.
- Testes reclassificados nas classes existentes: contratos textuais históricos de admin seed, Docker obrigatório, GED pronto, rotas antigas de menu e outbox legado passaram a validar o contrato atual.
- `Sigov.IntegrationTests` foi adicionado a `sigov.sln`; a suíte agora participa do restore/build/test padrão da solução.
- Dependências: `Testcontainers.PostgreSql` atualizado de 3.10.0 para 4.14.0 para remover bloqueio de `SSH.NET 2023.0.0` vulnerável.

## Evidência local RC51.00

- `dotnet restore sigov.runtime.slnf --locked-mode`: PASS.
- `dotnet build sigov.runtime.slnf -c Release --no-restore --nologo -warnaserror`: PASS, 0 erros, 0 avisos.
- `dotnet restore sigov.sln --locked-mode`: PASS.
- `dotnet build sigov.sln -c Release --no-restore --nologo -warnaserror`: PASS, 0 erros, 0 avisos.
- `dotnet test tests/Sigov.UnitTests/Sigov.UnitTests.csproj -c Release --no-build`: PASS, 371/371.
- `dotnet test tests/Sigov.ApiTests/Sigov.ApiTests.csproj -c Release --no-build`: PASS, 100/100.
- `dotnet test tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj -c Release --no-build`: PASS, 123/123.
- `pwsh -NoProfile -File scripts/check-migration-governance.ps1 -StaticOnly`: PASS estático; P0 runtime permanece BLOCKED por PostgreSQL 16.
- `bash scripts/check-tracked-artifacts.sh`: PASS.
- `bash scripts/check-api-route-conflicts.sh`: PASS, nenhum conflito direto em 630 rotas API.
- `python3 scripts/validate-rc50-80.py`: PASS, 175 migrations, 4 baselines e 1006 views verificados.
- `git diff --check`: PASS.
- `python -m json.tool database/postgres/migrations/manifest.json`: PASS.
- `npx --yes yaml-lint .github/workflows/*.yml`: PASS.
- `git ls-files | rg '(^|/)(bin|obj|\.vs)(/|$)|\.(trx|pdb|suo|user)$'`: PASS, sem artefatos rastreados.
- `rg -n '^(<<<<<<<|=======|>>>>>>>)' src database tests docs .github`: PASS, sem marcadores de conflito.
- `docker version`: BLOCKED, cliente 29.4.3 instalado, daemon `dockerDesktopLinuxEngine` indisponível.

## Bloqueios RC51.00

- `BLOCKED: PostgreSQL 16 vazio, reaplicação, upgrade legado e equivalência semântica completa não foram executados localmente porque o Docker Engine não está acessível neste host e não há instância PostgreSQL 16 descartável configurada.`
- `BLOCKED: Swagger runtime HTTP 200, login CPF/CNPJ/e-mail, MinhaCentral, logout, revogação, dois tenants, tenant suspenso e fluxo industrial runtime não foram executados porque dependem do P0 PostgreSQL 16 aprovado.`
- P1 SaaS Admin funcional e P2 Ordem de Produção industrial integrada não foram iniciados além das correções de autenticação/catálogo necessárias ao P0.

Data: 2026-09-09. RC50.99. Estado: PARCIAL / BLOCKED; P0 não aprovado e fases posteriores não iniciadas.

## Fechamento RC50.99

- Branch final: `codex/rc50-99-foundation-saas-industry`, baseada em `f6bb7f24df58620d3c898d3f28f8b460d8c366b1`.
- Governança: 184 SQLs, 174 entradas e dez órfãos classificados; validação estática PASS.
- Baseline: 170 migrations incluídas e quatro excluídas por contrato explícito. Aplicação vazia e reaplicação idempotente PASS no PostgreSQL 18 diagnóstico; ledger com 170 versões.
- Migrations publicadas `20260902010000` (validador UUID de Compras) e `20260903130000` (LicitaPro) preservadas e retiradas da execução automática/baseline, com migrations forward-only posteriores ativas.
- Restore locked e build Release `-warnaserror`: PASS. UnitTests: 371/371. Swagger runtime: 11/11 e HTTP 200. ApiTests: 88/100, com 12 contratos estáticos históricos falhando.
- `BLOCKED: aplicação/reaplicação, upgrade legado e equivalência no PostgreSQL 16 não executados porque o Docker Desktop local não iniciou e não há outra instância PostgreSQL 16 descartável disponível.`
- `BLOCKED: login CPF/CNPJ/e-mail, Minha Central, logout, revogação, tenant suspenso, dois tenants e fluxo industrial não executados porque dependem do P0 de banco oficial aprovado.`
- Nenhum commit e nenhum PR foram criados. Artefatos `.vs`, `bin` e `obj` já rastreados no commit base foram preservados e não devem ser staged.

Próximo comando exato: em PostgreSQL 16 descartável, executar duas vezes `psql -X -v ON_ERROR_STOP=1 -f database/postgres/script_completo.sql`; depois executar upgrade legado e comparação de schema.

## Registro anterior da fase P0

## Git e preservação

- Repositório: C:/MNSOFT/SIGOV-PLUS. Seis projetos src, database/postgres/migrations e tests confirmados.
- Inicial: codex/evolucao-saas-industria-360, HEAD e5c7e6c5ef782b38c17a6c3822c779e67ec9b3a9, sem upstream.
- Remoto: https://github.com/devmnsoft/SIGOV-PLUS.git.
- Commit local e5c7e6c sem mesmo SHA remoto; árvore idêntica à publicação 9f6618d, incorporada na main pelo PR #376.
- Fetch seguro: origin/main avançou para 5ef7516c; nenhuma diferença de árvore em relação ao checkout inicial.
- Branch desta fase: codex/p0-governanca-migrations, criada de origin/main (tracking inicial origin/main).
- Estado inicial: sete appsettings modificados; backups SQL, .vs, bin/obj, .github/copilot-instructions.md, migration mobile e lock de integração não rastreados. São preexistentes e não pertencem a esta fase.
- Branches disponíveis inspecionadas com git branch -avv; main e branch anterior locais, além das branches remotas históricas. Inventário exato será registrado junto às evidências.

## Achados iniciais

184 SQLs e 174 entradas no manifesto; dez órfãs; zero versões duplicadas no manifesto; um prefixo incompatível. Histórico usa knownChecksums com pós-condições.
sigov.sln contém somente runtime: dotnet test nessa solução não é evidência de testes executados. Swagger já possui teste runtime, que será reaproveitado.

## Ambiente

SDK efetivo 10.0.400 por rollForward latestFeature; 10.0.100 não instalado. PostgreSQL local 18, sem PostgreSQL 16; Docker CLI existe, daemon ausente. ConnectionStrings__DefaultConnection e senhas PostgreSQL não fornecidas ao processo.

BLOCKED: banco vazio/legado PostgreSQL 16 não executado porque não há instância descartável 16 disponível.
BLOCKED: login/isolamento runtime não executados porque dependem de banco validado e contas de teste.

## Escopo ativo

Governança de migrations, build e testes existentes. P1/P2/P3/P4 e GED não iniciados. Nenhuma migration histórica será alterada.
# Correção de roteamento e shell Web — 2026-09-15

## Diagnóstico confirmado

| Arquivo | Problema observado | Impacto | Correção |
|---|---|---|---|
| `ExecutiveOperationsController.cs` e `GovernancaTransversalController.cs` | Duas actions `GET` publicavam `/QualidadeDados` e outras duas publicavam `/IntegracoesInternas`. | O matcher encontrava candidatos equivalentes e lançava `AmbiguousMatchException` antes da autorização/action. | A central transversal persistida tornou-se canônica em `/Governanca/QualidadeDados` e `/Governanca/IntegracoesInternas`; os caminhos legados são aliases GET explícitos no mesmo controller e redirecionam permanentemente. |
| `_Sidebar.cshtml` | Links apontavam para os padrões ambíguos. | Navegação autenticada reproduzia a falha. | Links agora apontam diretamente para as rotas canônicas, sem depender do alias. |
| `sigov-ui.js` | O clique de abertura/fechamento do drawer era registrado duas vezes e o drawer não continha/devolvia o foco. | Comportamento instável em telas estreitas e navegação incompleta por teclado. | Um único manipulador delegado controla o drawer; Escape, backdrop, contenção e devolução de foco compartilham o mesmo estado. |
| CSS do shell | A sidebar clara contrariava o token existente de navegação e larguras estavam repetidas em valores literais. | Contraste/contexto visual inconsistentes e manutenção de responsividade frágil. | Largura e altura de controle foram tokenizadas; sidebar usa azul-marinho e tabelas rolam apenas em contêiner próprio. |

Não foi hipótese: a duplicidade foi confirmada nos atributos `HttpGet`. A equivalência funcional foi decidida pela origem dos dados: `GovernancaTransversalController` usa `ITransversalGovernancaService`, autorização persistida, tenant e ocorrências reais; as actions removidas exibiam apenas o resumo executivo genérico.

## Evidência e limite do ambiente

- O teste existente `WebRuntimeSmokeTests` foi ampliado (sem nova classe) para inicializar o host real, auditar `EndpointDataSource`, exigir um único endpoint GET por padrão, conferir controller canônico/autorização e requisitar rotas canônicas e aliases como usuário anônimo.
- `dotnet` não está instalado neste container (`dotnet: command not found`); restore, build, compilação Razor e testes permanecem **BLOCKED** neste ambiente e a entrega não está declarada homologada.
- Não houve alteração de schema ou migration.

Próximo item exato do backlog: executar o gate Web com .NET SDK `10.0.100` e PostgreSQL 16, autenticar um usuário com e sem `governanca.qualidade.visualizar` e capturar as larguras 360, 390, 768, 1024, 1366 e 1920 px.

## Evolução de navegação e formulário — 2026-09-15

| Arquivo | Achado observado | Impacto | Correção aplicada |
|---|---|---|---|
| `_Sidebar.cshtml` | Todos os grupos renderizados formavam uma única lista extensa; não havia seleção de contexto funcional. | Alto custo de varredura e navegação especialmente ruim no drawer móvel. | O próprio conjunto de grupos já autorizado/renderizado alimenta um seletor de módulo; apenas o módulo corrente fica visível e a busca temporariamente percorre todos os destinos disponíveis. |
| `sigov-ui.js` | Estado compacto era persistido globalmente e não existia estado de módulo. | Preferência podia atravessar contextos, além de não haver segunda camada contextual. | Seleção de módulo é persistida somente na chave composta usuário/tenant/entidade/exercício; rota ativa vence preferência antiga e módulos removidos são descartados. |
| `_Navbar.cshtml` | Ausência de claims de entidade/exercício era apresentada como entidade “padrão” e ano do relógio. | A interface afirmava um contexto operacional não selecionado. | O cabeçalho agora informa explicitamente contexto/entidade/exercício não selecionados, sem fabricar contexto. |
| `Requisicoes/Nova.cshtml` e editor JavaScript | Formulário real não tinha resumo focável, indicação/ajuda acessível ou antiforgery disponível ao cliente; erros de API não recebiam foco. | Erros eram difíceis de localizar por teclado/leitor de tela, embora o serviço já preservasse idempotência e versão. | Campos receberam labels/ajuda/limites, agrupamento semântico, resumo, foco de erro, token antiforgery e bloqueio de envio preservado; rascunho continua reaproveitando os valores persistidos. |

Hipótese não promovida a correção: o catálogo histórico da sidebar contém grupos legados cuja contratação precisa ser comprovada em runtime; esta alteração não transforma visibilidade em autorização e não altera o avaliador persistente. Não houve migration. `node --check`, `git diff --check` e varreduras estáticas passaram. Restore/build/Razor/testes e capturas continuam **BLOCKED** porque o container não dispõe do SDK .NET 10 nem de host Web executável.

Próximo item exato: executar `WebRuntimeSmokeTests` e a jornada autenticada em PostgreSQL 16; validar o seletor e o formulário em 360/390/768/1024/1366/1920 px e zoom 200%, então confrontar cada grupo legado renderizado com a decisão do avaliador canônico antes de promover a navegação.
# Programação operacional de ordens — 2026-09-15

## Retomada comprovada

- Branch inicial `work`, HEAD `c237e39ae5fc44c70dc4b754bde1aca9bae3d3ba`; árvore limpa. Nenhum remoto ou upstream estava configurado no checkout.
- `global.json` exige SDK 10.0.100 com `latestFeature`; projetos configuram `net10.0`/C# 14, ASP.NET Core MVC/Razor, Dapper e Npgsql.
- Disponíveis: Git, Node 20 e npm. Ausentes: `dotnet`, `psql`, Docker/Podman e navegador Chromium/Playwright. Build, Razor, suites, PostgreSQL 16, Swagger/rotas runtime e capturas ficaram `BLOCKED`, sem declaração de homologação.

## Revisão e classificação

| Jornada | Estado nesta execução | Evidência/limite |
|---|---|---|
| Contratação/suspensão, usuários, perfis e sessões | implementada sem validação | Serviços/ADRs persistidos encontrados; runtime de suspensão, revogação e dois tenants bloqueado. |
| Ordens de serviço, programação e apontamentos | parcial | Contratos, Dapper, API e Razor reais existem; esta fatia corrigiu programação/reprogramação, mas o gate runtime segue bloqueado. |
| Inspeções e não conformidades | parcial | Há jornadas específicas por módulo, sem prova integrada com o encerramento desta OS. |
| Estoque | parcial | Consumo/devolução canônicos existem, sem prova PostgreSQL 16 nesta execução. |
| Minha Central | implementada sem validação | Consulta persistida e falha explícita existentes; ainda não incorpora a lista operacional de OS. |
| Layout e navegação | implementada sem validação | MVC/Razor/Bootstrap preservados; programação ganhou lista responsiva e formulário acessível, sem captura por falta de runtime. |

## Regras confirmadas e implementadas

- Programação aceita `ABERTA`, `EM_TRIAGEM`, `AGENDADA`, esperas e `REABERTA`; `CONCLUIDA`/`CANCELADA` não são reabertas pela agenda. Datas realizadas permanecem intocadas.
- Intervalos são semiabertos (`inicio < outroFim && fim > outroInicio`): término igual ao próximo início não conflita.
- Reprogramação exige motivo, preserva o agendamento anterior por inativação auditável e grava o novo registro na mesma transação da versão da ordem.
- Responsável é selecionado pelo cadastro canônico `os_tecnico`, dentro do tenant e não inativo. Atribuição não concede permissão; API mantém as policies `os.ordens.agendar`/`visualizar`.
- O lock da ordem, a revalidação de versão, a consulta de sobreposição e a gravação ocorrem na mesma transação. Sobreposição de responsável é revisão autorizável, não uma afirmação de indisponibilidade; a ausência de calendário não confirma disponibilidade.
- A tela oferece lista acessível e formulário por teclado, filtros de período/responsável/local/prioridade, estados de carregamento/vazio/erro e confirmação. Não foi introduzido drag-and-drop nem calendário como fonte de estado.

## Decisões pendentes e próximo item

Não existe no modelo legado desta OS vínculo canônico de ativo/recurso exclusivo, entidade/exercício, calendário institucional, feriados ou política persistida que torne toda sobreposição de pessoa bloqueante. Esses dados não foram inventados. A atribuição em lote também não foi criada sem contrato canônico de atomicidade. Indicadores existentes ainda usam `prazo_sla`; não foram renomeados nem apresentados como novos indicadores operacionais.

Próximo item exato: criar uma RC própria, após Gate A, para definir no banco a política de disponibilidade/sobreposição e o vínculo multi-entidade/recurso exclusivo da OS legada; então integrar Minhas Atividades e encerramento com inspeções/não conformidades/reservas canônicas e validar dois tenants em PostgreSQL 16. GED permanece por último.

---

Data: 2026-09-15. Minha Central e distribuição — filtros pessoais persistidos. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

- Estado inicial: branch `work`, HEAD `f7a7e199784983c3c0d5bcb47526db7c36cfca1b`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; Git, Node e `jq` disponíveis; `dotnet`, PostgreSQL/`psql`, Docker, PowerShell, `yq` e `shellcheck` ausentes.
- Minha Central ganhou filtro de estado permitido, contagem coerente com o conjunto filtrado, paginação SQL e ordenação determinística. Itens mostram módulo e identificação persistidos; ausência de prazo continua sem classificar atraso.
- Central e lista de distribuição agora salvam, atualizam por nome, renomeiam, excluem e definem filtro padrão no repositório canônico `usuario_preferencia`, sob chave versionada, tenant, usuário e tela. Parâmetros são validados por listas fechadas; filtro não concede autorização.
- Distribuição ganhou cabeçalho contextual, barra de filtro, estado vazio e paginação que preserva o estado. A autorização canônica do controller continua sendo reavaliada antes da consulta.
- Sem migration: foi reutilizada a persistência canônica existente. GED não foi alterado.
- Validação estática: `git diff --check`, busca de conflitos e sintaxe JavaScript passaram. **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16, dois tenants, sessão/revogação, navegador e capturas nas seis larguras, pois os runtimes não existem no host.
- Próximo item exato: executar Gate A no SDK 10.0.100/PostgreSQL 16 e provar os filtros com dois tenants, revogação e reload; depois substituir a projeção genérica `pendencia_operacional` por adaptadores de leitura das seis fontes canônicas, com falha parcial explícita por fonte.
