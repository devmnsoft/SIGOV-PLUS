# Matriz de fluxos verticais

Corte: 2026-09-09. A matriz mede evidência conjunta; existência de controller, view ou tabela isolada não promove um fluxo.

| Ordem | Fluxo | Estado RC50.99 | Evidência necessária para avançar |
|---|---|---|---|
| P0.1 | Migrations e baseline | PASS (ciclo PG16 oficial executado na RC-EVO-A) | Ciclo completo no PostgreSQL 16 oficial (container `postgres:16`): instalação limpa com ledger 214/214 e última 20261007130000, reexecução idempotente no-op, upgrade legado de `7ece53c1` via sidecar `db-migrations` aplicando exatamente as 2 migrations pendentes, e equivalência de schema limpo == legado+upgrade (única diferença: ledger próprio do sidecar). Evidência: `docs/evidencias/rcevoa/relatorio_s11_instalacao_limpa_upgrade_legado.md`. Permanece a lacuna preexistente de reconciliação do ledger do sidecar para o canônico (item 6 BLOCKED do gate RC50.68A). |
| P0.2 | Build e testes | PARCIAL | Build Release está verde; suíte UnitTests 371/371; ApiTests 88/100, com 12 contratos estáticos históricos falhando. |
| P0.3 | Swagger e rotas | PARCIAL | Swagger runtime e GET do JSON passaram; auditoria integral de conflitos ainda depende do gate P0 fechado. |
| P0.4 | Autenticação e isolamento | AGUARDA_GATE | Executar login CPF/CNPJ/e-mail, Minha Central, logout, revogação, tenant suspenso e dois tenants com banco oficial validado. |
| P1 | Catálogo, entitlements e Administração SaaS | AGUARDA_GATE | Somente após P0 integralmente verde. |
| P2 | Indústria: cadastros → BOM/roteiro → OP → estoque → apontamento → qualidade → custos | AGUARDA_GATE | Somente após P1; executar um fluxo vertical real por vez. |
| P3 | Indústria avançada | AGUARDA_GATE | Core industrial funcional e homologado. |
| P4 | Demais domínios | AGUARDA_GATE | Ordem registrada em `BACKLOG_EXECUTAVEL.md`, sem implementação superficial paralela. |
| Final | GED | AGUARDA_GATE | Último módulo, salvo adiamento formal das pendências anteriores. |

Próximo comando normativo: o ciclo de 2026-10-08 (instalação limpa duas vezes + ensaio de upgrade legado + comparação de schema em PostgreSQL 16) foi executado e passou; fechar P0.1 exige apenas reconciliar o ledger do sidecar (`sigov.docker_schema_migrations`) com o canônico (`sigov.schema_migrations`) no fluxo oficial do runner (gap RC50.68A item 6).
