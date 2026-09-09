# Backlog executável

Estados: APROVADO (escopo autorizado), EM_EXECUCAO, VALIDADO (com evidência), BLOCKED (impedimento) e AGUARDA_GATE. Não confundir escopo aprovado com gate aprovado.

| Ordem | Item | Estado inicial | Aceite |
|---|---|---|---|
| 1 | P0.1-A inventário e gate estático de governança das migrations | EM_EXECUCAO | 184 arquivos classificados, checksums, prefixos, duplicidades, compatibilidades, política lexical, histórico e scripts verificados |
| 2 | P0.1-B convergência em PostgreSQL 16 vazio/legado | APROVADO | Aplicar/reaplicar manifesto, pós-condições, equivalência de schema e API/Worker |
| 3 | P0.2 build Release locked e suites realmente executadas | APROVADO | Zero erros/avisos e nenhum falso PASS por ausência de testes |
| 4 | P0.3 ApiExplorer/Swagger/OpenAPI/rotas | APROVADO | Todas as actions, duplicidades, IDs e HTTP /swagger/v1/swagger.json |
| 5 | P0.4 login, sessões, cache, revogação e dois tenants | APROVADO | Login e-mail/CPF/CNPJ, seleção segura, cookie, logout, senha, bloqueio/suspensão, acesso cruzado negado |
| 6 | P1.1–P1.6 catálogo, entitlements, preços, administrações e menu | AGUARDA_GATE | P0 integral aprovado e fluxo SaaS real |
| 7 | P2.1–P2.8 Indústria por fluxo vertical | AGUARDA_GATE | P1 aprovado; cadastros → BOM/roteiro → OP → estoque → apontamento → qualidade → custos → integrações |
| 8 | P3 Indústria avançada | AGUARDA_GATE | Core industrial validado |
| 9 | P4 demais módulos na ordem do contrato | AGUARDA_GATE | Auditoria por fluxo, sem telas decorativas |
| 10 | GED | AGUARDA_GATE | Último módulo; anteriores sem PARCIAL/ESTRUTURA pendente, salvo adiamento formal |

Próximo item exato: concluir P0.1-A e registrar inconsistências reais sem reescrever migrations históricas.

