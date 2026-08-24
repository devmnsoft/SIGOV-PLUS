# Plano de implementação ativo

## RC50.68 — Control Plane e autorização persistente

- [x] RC50.68A/B: contrato persistente e avaliador Dapper fail-closed, com `NEGAR` prevalecendo.
- [x] RC50.68C/D: contexto operacional seguro e dashboard SuperAdmin.
- [x] RC50.68F-R2: estabilizada a administração executável (SQL canônico, DI, Razor/JS e antiforgery);
  administração SuperAdmin de perfis, grupos, permissões e vínculos contextuais,
  incluindo vigência, escopos, efeito, alçada, estado e auditoria antes/depois.
- [ ] Promoção RC50.68: **BLOCKED** até CI real integralmente verde. A falta de autenticação que
  bloqueou a RC50.68E-R4 não é contornada nem considerada aprovada por esta entrega.
- [ ] RC50.69: não iniciada e condicionada à promoção da RC50.68.

O detalhamento histórico permanece em [`PLANO_MESTRE_SIGOV_PLUS.md`](PLANO_MESTRE_SIGOV_PLUS.md).
