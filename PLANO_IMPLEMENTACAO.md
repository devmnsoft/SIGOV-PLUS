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
- [ ] Fechar as expansões LicitaPro IA, Fiscaliza360, Obras360, DefesaCivil360,
  Ativos360, Cidadão360 e Jurídico360 nas FUNCs existentes.
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
