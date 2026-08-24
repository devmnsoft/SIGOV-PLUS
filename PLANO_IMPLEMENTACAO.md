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
