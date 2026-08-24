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
