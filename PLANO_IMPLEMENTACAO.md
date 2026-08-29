# Plano de implementação ativo

## RC50.68 — Control Plane e autorização persistente

- [x] RC50.68A/B: contrato persistente e avaliador Dapper fail-closed, com `NEGAR` prevalecendo.
- [x] RC50.68C/D: contexto operacional seguro e dashboard SuperAdmin.
- [x] RC50.68F-R2: estabilizada a administração executável (SQL canônico, DI, Razor/JS e antiforgery);
  administração SuperAdmin de perfis, grupos, permissões e vínculos contextuais,
  incluindo vigência, escopos, efeito, alçada, estado e auditoria antes/depois.
- [ ] Promoção RC50.68: **BLOCKED**. A RC50.68E-R6 executou o gate local disponível: validações
  estáticas passaram, mas .NET, `psql`, PostgreSQL 16 seguro, smoke e PowerShell ficaram BLOCKED.
  Não houve FAIL observado, nenhum PASS foi inferido e o CI oficial não foi consultado. É necessária
  uma execução local integralmente verde e a avaliação separada do gate oficial.
- [ ] RC50.69: não iniciada e condicionada à promoção da RC50.68.

O detalhamento histórico permanece em [`PLANO_MESTRE_SIGOV_PLUS.md`](PLANO_MESTRE_SIGOV_PLUS.md).

## Roadmap estratégico 2026

- [x] Matriz de integração consolidada sem duplicar FUNC01, FUNC03, FUNC04,
  FUNC13, FUNC15, FUNC17 e FUNC19.
- [ ] Consolidar o núcleo transversal de fiscalização, evidências, campo e
  sincronização offline após a promoção da RC50.68.
- [ ] Fechar as expansões LicitaPro IA, Fiscaliza360, Obras360, Ativos360,
  Cidadão360 e Jurídico360 nas FUNCs existentes.
- [x] EXP19 DefesaCivil360 implementada dentro do FUNC19; homologação depende dos gates de ambiente.
- [ ] Implementar FUNC21 SST 360.
- [ ] Implementar FUNC22 Carbono360.
- [ ] Implementar FUNC23 Energia360.
- [ ] Implementar FUNC24 Royalties360.

Critérios, integrações e regras de negócio estão em
[`docs/ROADMAP-MODULOS-ESTRATEGICOS-2026.md`](docs/ROADMAP-MODULOS-ESTRATEGICOS-2026.md).
Itens planejados não podem aparecer como disponíveis no catálogo comercial ou
no menu operacional antes da persistência, autorização, telas, testes existentes,
smoke e documentação estarem homologados.

## FUNC01 — trilha funcional paralela

- [x] Implementação de Patrimônio, Inventário e Responsabilidade Patrimonial entregue em código com PostgreSQL/Dapper, MVC/API, dashboard, auditoria, LGPD e autorização persistida.
- [ ] Homologação integral depende dos gates disponíveis de .NET 10 e PostgreSQL 16.
- A FUNC01 avança produto real e não altera promoções: RC50.68 continua **BLOCKED** por ambiente/CI e RC50.69 continua não iniciada/não promovida.
- Manual: [`docs/FUNC01-PATRIMONIO-INVENTARIO.md`](docs/FUNC01-PATRIMONIO-INVENTARIO.md).

## FUNC02 — trilha funcional paralela

- [x] Almoxarifado, catálogo, locais, saldos, movimentos, requisições, dashboard, CSV auditado e pendência patrimonial entregues em código.
- [ ] Homologação integral depende dos gates oficiais de .NET 10, PostgreSQL 16 e smoke autenticado.
- FUNC02 não promove a RC50.68, que continua **BLOCKED**, e não inicia nem marca a RC50.69.
- Manual: [`docs/FUNC02-ALMOXARIFADO-ESTOQUE-REQUISICOES.md`](docs/FUNC02-ALMOXARIFADO-ESTOQUE-REQUISICOES.md).

## FUNC03 — Compras Públicas (trilha funcional paralela)
Implementados fornecedores, solicitações, processos e fases, cotações, julgamento, contratos, atas e recebimentos rastreáveis, com PostgreSQL/Dapper, RBAC persistente, auditoria e telas MVC. A RC50.68 permanece **BLOCKED** pelo ambiente oficial; RC50.69 não foi iniciada. Esta entrega não promove release.

## FUNC04 — Gestão de Frotas (trilha funcional paralela)

- [x] Veículos, motoristas, utilizações, abastecimentos, manutenções, ordens de serviço, documentos, custos e alertas entregues com PostgreSQL/Dapper, MVC/API e RBAC persistente.
- [x] Integrações rastreáveis com bem patrimonial, contratos/fornecedores e baixa transacional do Almoxarifado, com bloqueio por saldo insuficiente.
- [ ] Homologação runtime/PostgreSQL oficial permanece condicionada ao ambiente. RC50.68 continua **BLOCKED** e RC50.69 não foi iniciada; FUNC04 não promove release.

## FUNC05 — Educação e Gestão Escolar (trilha funcional paralela)

- [x] Schema FUNC05 idempotente, RBAC persistente, Dapper/API e telas MVC para gestão escolar, i-Diário, pré-matrícula e Portal Pais/Alunos.
- [x] LGPD, auditoria, vagas, duplicidade de matrícula e bloqueios de período/nota documentados.
- [ ] RC50.68 permanece **BLOCKED** pelos gates oficiais; RC50.69 não foi iniciada. GED/InovaGED foi adiado para a etapa final.

## FUNC06 — Saúde e Atenção Básica (trilha funcional paralela)

- [x] Unidades, pacientes, profissionais/equipes, agenda, acolhimento, prontuário, vacinação, farmácia, regulação, dashboard e CSV entregues sobre PostgreSQL/Dapper e RBAC persistente.
- [x] Regras críticas defendidas no banco, com auditoria/LGPD e integração de saldo do Almoxarifado quando existe vínculo seguro.
- [ ] Homologação PostgreSQL 16 e smoke autenticado dependem do ambiente oficial. RC50.68 continua **BLOCKED**; RC50.69 não foi iniciada; GED/InovaGED segue adiado.

# RC50.80 — fechamento geral (29/08/2026)

- [x] Consolidar escopo, contratos de segurança, LGPD, CSV e critérios de promoção.
- [x] Adicionar gate determinístico de manifest, checksums, baselines e antiforgery.
- [x] Registrar a entrega e o roteiro de homologação sem simular integrações.
- [ ] BLOCKED: build .NET 10, PostgreSQL 16 e smoke autenticado dependem das
  ferramentas e do ambiente de homologação indisponíveis neste checkout.
# RC50.68 — desbloqueio e fundação transversal (26/08/2026)

- Baseline, manifest e scripts distribuíveis foram sincronizados com a migration corretiva `20260826120000`.
- A fundação canônica agora define metadados de evidência com referência opcional ao GED e fila de sincronização idempotente, sem arquivo fictício, worker ou envio externo.
- Contratos compartilhados de evidência, seleção de entidades relacionadas e sincronização foram adicionados; relatórios CSV passam a dispor de escape contra formula injection.
- FUNC21, FUNC22, FUNC23 e FUNC24 permanecem planejadas e indisponíveis. Nenhum módulo, rota ou item de menu foi liberado.
- Gates de .NET, PostgreSQL e smoke ficaram **BLOCKED** neste ambiente porque `dotnet` e `psql` não estão instalados e não há banco/credencial de homologação.

## EXP03 — LicitaPro IA / FUNC03

- [x] Persistência aditiva e idempotente, permissões e índices.
- [x] Dashboard, fontes, importações, oportunidades e vínculo a processo.
- [x] Portal, documentos, checklist, análise, agenda, alertas, CSV e auditoria.
- [x] Dapper parametrizado, antiforgery, validação server-side e seleção de relacionamentos.
- [ ] BLOCKED: `dotnet build --no-restore` não executado porque o SDK dotnet não está instalado no ambiente.
- [ ] Smoke executável depende de runtime e PostgreSQL configurado.

## CORR03 — fechamento LicitaPro IA no FUNC03

- [x] Consolidar navegação, dashboard e telas reais do LicitaPro no FUNC03.
- [x] Remover entrada manual de identificadores relacionais e preservar ModelState.
- [x] Aplicar validações server-side de oportunidade, documento e agenda.
- [x] Corrigir filtros e cabeçalhos/proteção de CSV.
- [x] Sincronizar migration corretiva, manifest e scripts PostgreSQL consolidados.
- [x] Documentar limite explícito: sem Fiscaliza360 e sem FUNC21–FUNC24.

## EXP13 — Obras360

Implementação do fechamento operacional do FUNC13 entregue por migration corretiva/funcional, repositório Dapper, MVC/Razor, RBAC persistido, CSV seguro e documentação. Integrações externas preservam a autoridade dos módulos de origem e não usam fallback artificial.

### CORR13 — Obras360

Fechamento técnico concluído no código e nos artefatos SQL: validações de domínio, autorização fail-closed, read models Dapper, formulários Razor e exportações CSV revisados. A execução de build, PostgreSQL e smoke ficou registrada como `BLOCKED` por ausência das ferramentas no ambiente, sem simulação de sucesso.

## EXP08 — Ativos360

- [x] Integrar dashboard e navegação a patrimônio, almoxarifado e frotas existentes.
- [x] Complementar transferência, depreciação, baixa, motoristas, rotas e alertas operacionais.
- [x] Bloquear operações de frota inativa/baixada e valores negativos no PostgreSQL.
- [x] Persistir permissões ATIVOS_* e sincronizar migration, manifest e scripts completos.
- [x] Entregar experiência responsiva, estados vazios e central de relatórios sem ID manual.

## EXP04 — Cidadão360

- [x] Portal, catálogo persistido, abertura e consulta segura de protocolos.
- [x] Integração com Atendimento, Ouvidoria, workflow, pessoa, documentos, agenda e avaliação.
- [x] Migration, permissões, scripts completos, LGPD e documentação.
- [ ] Homologar adaptadores externos e upload GED quando contratos oficiais estiverem configurados.

## EXP06 — Jurídico360

Implementado sobre o FUNC17 e contratos oficiais: persistência complementar idempotente, carteira e contencioso, execução fiscal/CDA, consultivo, prazos, agenda, acordos, precatórios/RPV, documentos, publicações, permissões, auditoria, CSV seguro e design responsivo. Validação PostgreSQL e build permanecem condicionados às ferramentas registradas na entrega.

### EXP09 — SST360

Implementado o núcleo integrado de Saúde e Segurança do Trabalho: banco idempotente e baselines sincronizados, autorização persistida, dashboard contextual, rotas funcionais e CRUD MVC de ASO com validações server-side e proteção LGPD.

### EXP23 — Energia360

- [x] Persistência PostgreSQL idempotente, permissões e baselines sincronizados.
- [x] Dashboard e operações MVC/Razor com Dapper, contexto multiempresa e validação no servidor.
- [x] Iluminação, geração, créditos, eficiência, alertas transparentes e CSV seguro.
- [ ] Homologar integrações externas somente quando adaptadores e cadastros oficiais estiverem configurados.

### EXP24 — Royalties360

- [x] Persistência PostgreSQL idempotente e catálogo de permissões.
- [x] Dapper parametrizado com isolamento por tenant e entidade.
- [x] MVC/Razor para dashboard, normas, receitas, aplicação, projetos, transparência e governança.
- [x] CSV seguro, documentação e scripts consolidados sincronizados.
- [ ] Homologar integrações externas somente após disponibilização de adaptadores reais.

### EXP13 — Saneamento360/SIGCOS

Implementado de forma evolutiva sobre as entidades `sigov.saneamento_*`, com migration idempotente, permissões no banco, rotas MVC/Razor, Dapper/Npgsql, auditoria LGPD e integrações reais condicionais. Não foram criados dados ou provedores de fallback.

## EXP11 Saúde360 + ACS360

Implementação funcional adicionada em 2026-08-29: schema territorial/ACS, sincronização offline, staging e-SUS, vigilâncias, rotas MVC e formulários validados. A autoridade continua no PostgreSQL e nas permissões persistidas. Integração ministerial e recursos nativos de câmera permanecem bloqueados até disponibilização de contrato/layout e aplicativo móvel real.

## EXP25 — GED360 / InovaGED Inteligente

- [x] Estrutura PostgreSQL idempotente, índices, checks e permissões.
- [x] Dashboard e busca documental MVC/Razor sob `/GED`.
- [x] Fluxos rastreáveis de OCR, assinatura, temporalidade, eliminação e acervo.
- [x] Auditoria LGPD e vínculo único com módulos de origem.
- [ ] Homologar motor OCR e provedor de assinatura no ambiente integrado.

## CORR25 — fechamento GED360/InovaGED

Fechamento técnico incorporado sem expansão de escopo: segurança de consulta e rotas, integridade PostgreSQL, LGPD e artefatos SQL sincronizados. Build e smoke autenticado devem ser confirmados em ambiente com .NET 10 e PostgreSQL 16 antes da promoção.

## RC50.81 — Homologação Enterprise

A fundação persistente de checklist, histórico, auditoria operacional e permissões transversais foi adicionada pela migration `20260829180000_rc50_81_homologacao_enterprise.sql`. Próximas etapas devem implementar as centrais MVC sobre esses contratos reais, sem fallback decorativo, e executar o baseline em PostgreSQL 16+ antes da homologação comercial.
