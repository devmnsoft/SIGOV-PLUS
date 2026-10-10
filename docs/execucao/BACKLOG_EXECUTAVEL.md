# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 186 SQLs / 176 manifesto / 172 auto / 172 baseline / 10 órfãs; gate artefatos PASS |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | EM_EXECUCAO | RC51.02O corrigiu o checksum canônico de `20260914120000` sem alterar a migration, preservou o valor anterior como histórico condicionado e tornou a validação de `knownChecksums`/probes fail-closed nos dois aplicadores; wrapper Bash continua somente validador; vazio+reapply+one-shot+equivalência têm evidência anterior, upgrade legado e reconfirmação desta alteração ainda BLOCKED |
| 3 | P0.2 build Release locked e suites realmente executadas | VALIDADO | build `-warnaserror` + testes 389/123/102 PASS em 2026-09-10 |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | VALIDADO | Swagger HTTP 200 (3 445 457 bytes); 630 rotas sem conflito direto |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | VALIDADO | Jornada ao vivo em PG16 (2026-10-09): login e-mail/CPF/CNPJ, Minha Central, logout (`motivo=LOGOUT`), revogação por mudança de identidade (auth_version obsoleta rejeita cookie), tenant suspenso bloqueia/religa e dois tenants isolados (sem vazamento de contexto); rastro em `sigov.auditoria_evento`; evidência `docs/evidencias/rcevo/relatorio_p0_4_jornada_autenticacao_isolamento_pg16.md` |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | VALIDADO | Gate B (RC-SAAS-AUT) validado ao vivo em PG16 em 2026-10-10 (driver `scripts/gate-b-saas-runtime.ps1`, 26/26): avaliador canônico fail-closed A/B/C/D, 403 padronizado (`motivo`) paridade Web+API, revogação paridade A5 (Web 302/API 401), admin global MNSOFT como autoridade do banco; dual-catalog consolidado (navegação `ICommercialModuleCatalogService`, decisão só em `Persistent*`; build `-warnAsError` 0w/0e + 508/158/158, 0 ignorados). Nota de escopo (regra 13): valida a vertical de autorização (condição de aceite do ADR); mutações comerciais, convites/ACEITE FUNCIONAL e troca de contexto com auditoria seguem cobertos deterministamente pelas suítes, ainda sem prova ao vivo. Evidência `docs/evidencias/rcevo/relatorio_gate_b_saas_aut_runtime_pg16.md` |
| 7 | P2.1–P2.8 Indústria OP vertical | AGUARDA_GATE | Gate C após A+B; telas ainda genéricas (`ModulePage`) |
| 7a | Fatias Compras/Almoxarifado, Jurídico, Educação | AGUARDA_GATE | Compras empresariais possui listagem filtrada/paginada, criação, edição versionada de rascunho, detalhe e envio transacional com histórico; aprovação configurável, pedido, recebimento/estorno e integração de estoque continuam aguardando Gates A+B+C. Educação ainda exige transferência histórica, correção auditada da chamada, índice por oferta e prova PostgreSQL 16 |
| 7b | Demais módulos na ordem do contrato | AGUARDA_GATE | Atualizar backlog após Gate D; GED por último |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |
| 11 | Programação operacional de OS | AGUARDA_GATE | Lista, filtros, seleção nominal, programação/reprogramação transacional e regras estáticas implementadas; faltam política persistida de recurso exclusivo/disponibilidade, entidade/exercício, lote, Minhas Atividades integrada e gate PostgreSQL 16/navegador. |

Próximo item exato: validar sintaxe do aplicador com `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; em PG16, provar banco vazio/reaplicação, rejeição de versão e checksum desconhecidos, aceitação do checksum histórico de `20260914120000` com pós-condição e upgrade legado formal. Revogação e tenant suspenso já têm evidência ao vivo em PG16 (relatório P0.4); falta ainda a prova explícita de acesso cruzado em API (TenantAccessGuard/RequireModule); Gate B (SaaS) **FECHADO em 2026-10-10** (RC-SAAS-AUT validado ao vivo em PG16, 26/26; `docs/evidencias/rcevo/relatorio_gate_b_saas_aut_runtime_pg16.md`). Liberado o Gate C, definir e persistir a política canônica de alçadas antes de implementar a caixa de aprovação; pedidos, recebimentos e estoque vêm na sequência. GED permanece por último.

## Próximo item após a cadeia de custódia patrimonial (2026-09-15)

Implementar, em migration forward-only sincronizada, o fluxo `SOLICITADO -> RECEBIDO/CANCELADO` com versão do bem, seleção nominal autorizada de unidade/responsável e testes concorrentes; em seguida preservar a fotografia temporal do escopo do inventário. GED permanece por último.

## Central e distribuição — checkpoint 2026-09-15

Filtros pessoais versionados para Minha Central e distribuição estão implementados sobre `usuario_preferencia`, com escopo tenant/usuário/tela, lista fechada de parâmetros, antiforgery e aplicação de padrão. Central possui total filtrado, paginação e ordenação determinística; distribuição preserva estado na paginação. Estado: **AGUARDA_GATE**. Isolamento de dois tenants, revogação e tenant/módulo suspenso já têm evidência ao vivo em PG16 (relatório P0.4); permanece provar persistência/reload dos filtros versionados em PostgreSQL 16; em seguida integrar as seis fontes operacionais canônicas com indisponibilidade parcial explícita, sem duplicar registros.
