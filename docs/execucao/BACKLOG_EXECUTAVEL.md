# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 185 SQLs / 175 manifesto / 171 auto / 171 baseline / 10 órfãs; gate artefatos PASS |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | EM_EXECUCAO | Vazio+reapply+one-shot+equivalência PASS (Podman); upgrade legado ainda BLOCKED |
| 3 | P0.2 build Release locked e suites realmente executadas | VALIDADO | build `-warnaserror` + testes 389/123/102 PASS em 2026-09-10 |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | VALIDADO | Swagger HTTP 200 (3 445 457 bytes); 630 rotas sem conflito direto |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | EM_EXECUCAO | Login/CPF/CNPJ/MinhaCentral/logout + 2 tenants hero PASS; revogação/suspenso/legado pendentes |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | AGUARDA_GATE | Gate B bloqueado até Gate A completo; dual `IModuleCatalogService` permanece |
| 7 | P2.1–P2.8 Indústria OP vertical | AGUARDA_GATE | Gate C após A+B; telas ainda genéricas (`ModulePage`) |
| 7a | Fatias Compras/Almoxarifado, Jurídico, Educação | AGUARDA_GATE | Gate D após A+B+C |
| 7b | Demais módulos na ordem do contrato | AGUARDA_GATE | Atualizar backlog após Gate D; GED por último |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: upgrade legado formal em PG16 + evidências de revogação/tenant suspenso/acesso cruzado API; só então Gate B (SaaS). Gates C–D e GED não avançam.
