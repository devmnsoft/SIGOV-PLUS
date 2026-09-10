# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 184 arquivos classificados, checksums, prefixos, duplicidades, compatibilidades, política lexical, histórico e scripts verificados |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | BLOCKED | PostgreSQL 16 descartável indisponível neste host; psql ausente e Docker daemon indisponível |
| 3 | P0.2 build Release locked e suites realmente executadas | VALIDADO | `dotnet build sigov.sln -c Release -warnaserror` e `dotnet test` 389/123/102 em 2026-09-10 |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | EM_EXECUCAO | Conflitos diretos: PASS em 630 rotas; Swagger HTTP 200 runtime segue bloqueado até PostgreSQL 16 |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | EM_EXECUCAO | Cookie compacto e snapshot request-scoped implementados; prova runtime e-mail/CPF/CNPJ BLOCKED até PostgreSQL 16 |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | EM_EXECUCAO | Catálogo `modulo_saas`, avaliador único e SaaS Admin de contratação implementados; ACEITE runtime BLOCKED até PostgreSQL 16 |
| 7 | P2.1–P2.8 Indústria por fluxo vertical | AGUARDA_GATE | P1 aprovado; cadastros → BOM/roteiro → OP → estoque → apontamento → qualidade → custos → integrações |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: disponibilizar PostgreSQL 16 descartável e concluir P0.1-B, Swagger 200 e login/isolamento. Depois, com todos os gates verdes: **RC51.03 — Ordem de Produção industrial integrada demanda/pedido → BOM versionada → roteiro versionado → disponibilidade e reserva atômica → liberação da OP → apontamento → consumo por lote → qualidade → entrada do acabado → custo real/variação → encerramento e rastreabilidade.** GED continua obrigatoriamente por último.
