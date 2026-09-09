# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | VALIDADO | 184 arquivos classificados, checksums, prefixos, duplicidades, compatibilidades, política lexical, histórico e scripts verificados |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | BLOCKED | PostgreSQL 18 diagnóstico passou vazio/reaplicação; repetir no 16, validar legado e equivalência |
| 3 | P0.2 build Release locked e suites realmente executadas | EM_EXECUCAO | Build e UnitTests verdes; resolver ou reclassificar 12 contratos estáticos históricos de ApiTests |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | EM_EXECUCAO | Swagger runtime 11/11 e HTTP 200; concluir auditoria integral após banco oficial |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | AGUARDA_GATE | Login e-mail/CPF/CNPJ, seleção segura, cookie, logout, senha, bloqueio/suspensão, acesso cruzado negado |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | AGUARDA_GATE | P0 integral aprovado e fluxo SaaS real |
| 7 | P2.1–P2.8 Indústria por fluxo vertical | AGUARDA_GATE | P1 aprovado; cadastros → BOM/roteiro → OP → estoque → apontamento → qualidade → custos → integrações |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: disponibilizar PostgreSQL 16 descartável e concluir P0.1-B sem reescrever migrations históricas.
