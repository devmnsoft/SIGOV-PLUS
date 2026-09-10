# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 184 arquivos classificados, checksums, prefixos, duplicidades, compatibilidades, política lexical, histórico e scripts verificados |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | EM_EXECUCAO | Vazio+reaplicação PASS em PG16.15 Podman:5433; equivalência/legado ainda BLOCKED |
| 3 | P0.2 build Release locked e suites realmente executadas | VALIDADO | `dotnet build` Api/Web Release e IconRegistryTests 6/6 em 2026-09-10 |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | VALIDADO | Swagger HTTP 200 (3 445 457 bytes) na API local contra PG16 Gate A |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | EM_EXECUCAO | Login e-mail/login/CPF/CNPJ + MinhaCentral + logout PASS; dois tenants/legado BLOCKED |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | AGUARDA_GATE | RC51.02C Gate 2 não iniciado; unificar Commercial→SaaS e completar Admin SaaS |
| 7 | P2.1–P2.8 Indústria OP vertical | AGUARDA_GATE | RC51.02C Gate 3 não iniciado; telas ainda genéricas (`ModulePage`) |
| 7a | Fatias Compras/Almoxarifado, Jurídico, Educação | AGUARDA_GATE | RC51.02C Gate 4 após Gates 1–3 |
| 7b | Demais módulos na ordem do contrato | AGUARDA_GATE | Atualizar backlog após Gate 4; GED por último |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: fechar equivalência one-shot e isolamento de dois tenants em PG16; só então Gate 2 (SaaS Admin). Gates 3–5 e GED não avançam enquanto Gate 1 estiver incompleto.
