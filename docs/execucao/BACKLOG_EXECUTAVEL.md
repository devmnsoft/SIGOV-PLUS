# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 184 arquivos classificados, checksums, prefixos, duplicidades, compatibilidades, política lexical, histórico e scripts verificados |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | BLOCKED | PostgreSQL 16 descartável indisponível neste host; psql ausente e Docker daemon indisponível |
| 3 | P0.2 build Release locked e suites realmente executadas | VALIDADO | `dotnet clean`, `restore --locked-mode`, `build -c Release -warnaserror` e `dotnet test sigov.sln -c Release --no-build` passaram em 2026-09-10 |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | EM_EXECUCAO | Conflitos diretos: PASS em 630 rotas; Swagger HTTP 200 runtime segue bloqueado até PostgreSQL 16 |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | AGUARDA_GATE | Login e-mail/CPF/CNPJ, seleção segura, cookie, logout, senha, bloqueio/suspensão, acesso cruzado negado |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | AGUARDA_GATE | Não iniciar antes de P0 integral aprovado em PostgreSQL 16 |
| 7 | P2.1–P2.8 Indústria por fluxo vertical | AGUARDA_GATE | P1 aprovado; cadastros → BOM/roteiro → OP → estoque → apontamento → qualidade → custos → integrações |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: disponibilizar PostgreSQL 16 descartável e concluir P0.1-B sem reescrever migrations históricas.
> Próximo item após P0 PostgreSQL 16 aprovado e o entitlement canônico funcional: **RC51.03 — Indústria: Ordem de Produção integrada demanda/venda → BOM/ficha técnica → roteiro → reserva de materiais → OP → apontamento → consumo → qualidade → produto acabado → estoque → custos → rastreabilidade.** GED continua obrigatoriamente por último.
