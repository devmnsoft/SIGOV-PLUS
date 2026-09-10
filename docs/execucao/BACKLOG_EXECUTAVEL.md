# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 184 arquivos classificados, checksums, prefixos, duplicidades, compatibilidades, política lexical, histórico e scripts verificados |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | EM_EXECUCAO | Vazio+reaplicação PASS em PG16.15:5433; equivalência/legado BLOCKED por instabilidade do Docker Desktop |
| 3 | P0.2 build Release locked e suites realmente executadas | VALIDADO | `dotnet build sigov.sln -c Release -warnaserror` e `dotnet test` 389/123/102 em 2026-09-10 |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | EM_EXECUCAO | Rotas 630 PASS; Swagger HTTP 200 obtido na API local contra PG16 Gate A (~3,4 MiB) |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | EM_EXECUCAO | Hash admin/superadmin PASS; login HTTP/MinhaCentral/isolamento BLOCKED (PG16 caiu no meio do teste) |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | AGUARDA_GATE | Gate B não avançado: exige Gate A runtime verde; catálogo Commercial e B2 completo pendentes |
| 7 | P2.1–P2.8 Indústria por fluxo vertical | AGUARDA_GATE | Gate C bloqueado até A e B verdes; telas ainda genéricas |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: PostgreSQL 16 descartável e Gate A runtime (vazio/reaplicação/legado/equivalência, Swagger 200, login/dois tenants). Gate B e Gate C não avançam enquanto A estiver BLOCKED. GED continua por último.
