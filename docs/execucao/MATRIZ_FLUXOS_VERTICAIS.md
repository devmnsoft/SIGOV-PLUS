# Matriz de fluxos verticais

Corte: 2026-09-09. A matriz mede evidência conjunta; existência de controller, view ou tabela isolada não promove um fluxo.

| Ordem | Fluxo | Estado RC50.99 | Evidência necessária para avançar |
|---|---|---|---|
| P0.1 | Migrations e baseline | BLOCKED | Repetir banco vazio, reaplicação, legado e equivalência no PostgreSQL 16 oficial. O ciclo vazio/reaplicação passou apenas no PostgreSQL 18 diagnóstico. |
| P0.2 | Build e testes | PARCIAL | Build Release está verde; suíte UnitTests 371/371; ApiTests 88/100, com 12 contratos estáticos históricos falhando. |
| P0.3 | Swagger e rotas | PARCIAL | Swagger runtime e GET do JSON passaram; auditoria integral de conflitos ainda depende do gate P0 fechado. |
| P0.4 | Autenticação e isolamento | AGUARDA_GATE | Executar login CPF/CNPJ/e-mail, Minha Central, logout, revogação, tenant suspenso e dois tenants com banco oficial validado. |
| P1 | Catálogo, entitlements e Administração SaaS | AGUARDA_GATE | Somente após P0 integralmente verde. |
| P2 | Indústria: cadastros → BOM/roteiro → OP → estoque → apontamento → qualidade → custos | AGUARDA_GATE | Somente após P1; executar um fluxo vertical real por vez. |
| P3 | Indústria avançada | AGUARDA_GATE | Core industrial funcional e homologado. |
| P4 | Demais domínios | AGUARDA_GATE | Ordem registrada em `BACKLOG_EXECUTAVEL.md`, sem implementação superficial paralela. |
| Final | GED | AGUARDA_GATE | Último módulo, salvo adiamento formal das pendências anteriores. |

Próximo comando normativo: disponibilizar PostgreSQL 16 descartável e executar duas vezes `psql -X -v ON_ERROR_STOP=1 -f database/postgres/script_completo.sql`, seguido do ensaio de upgrade legado e comparação de schema.
