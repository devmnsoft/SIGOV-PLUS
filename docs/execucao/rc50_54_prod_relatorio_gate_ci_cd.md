# RC50.54-PROD — relatório do gate CI/CD

Data de preparação: 2026-08-19. Decisão atual: **NÃO APTO até execução externa verde**.

1. Workflow criado: `.github/workflows/production-gate.yml`, acionado por PR para `main` e manualmente.
2. Jobs: static-validation, database-clean-apply, database-partial-apply, runtime-build, runtime-smoke e artifact-summary.
3. .NET: `10.0.100`, proveniente de `global.json`/setup-dotnet.
4. PostgreSQL: service `postgis/postgis:16-3.4`, com clientes `psql`, `pg_dump`, `pg_restore` obrigatórios.
5. Banco limpo: automatizado; resultado remoto pendente.
6. Banco parcial: banco `sigov_partial` com forma legada mínima de `obra_medicao`; resultado remoto pendente.
7. Validadores: manifest, três checks de índices e rotas foram incorporados; execução local descrita abaixo.
8. Warnings: 49/126/7 históricos classificados em P0/P1/P2 no documento de triagem.
9. P0 corrigidos: o gate não aceita ferramenta obrigatória ausente/`SKIP`, e verifica segredo em artifact. P0 aberto: usos históricos de `SELECT *` detectados; o gate os bloqueia até projeções explícitas seguras.
10. P1 pendentes: prova autenticada/persistente de permissões, LGPD, auditoria e exportações em homologação.
11. Build: clean/restore locked/build Release `-warnaserror` automatizados; execução externa pendente.
12. API: start HTTP e `/health` automatizados; resultado pendente.
13. Swagger: JSON em Development incluído nas páginas críticas; resultado pendente.
14. Web: start HTTP, login e páginas críticas automatizados; resultado pendente.
15. Worker: execução por 30 s aceita somente sucesso ou timeout controlado; resultado pendente.
16. Smoke: artifact RC50.54 e rejeição explícita de SKIP obrigatório; resultado pendente.
17. Backup: custom format/schema `sigov`; resultado pendente.
18. Restore: banco separado `sigov_restore`, seguido de verificação; resultado pendente.
19. Artifacts: logs de banco e `rc50-54-production-evidence`, sanitizados e publicados mesmo em falha.
20. Gate Windows: script/documentação criados; não executável neste host Linux sem ambiente Windows.
21. HTTP 501: busca estática é bloqueante para padrões explícitos; nenhum essencial foi encontrado pela execução local.
22. Menus 404: onze rotas Web são sondadas; resultado runtime pendente.
23. Dashboards 500: status fora de 200/302/401/403 falha; resultado runtime pendente.
24. Decisão: não apto, pois código de automação não substitui sua execução no GitHub e Windows.
25. RC50.55: executar ambos os gates, corrigir qualquer SQL/runtime P0, fechar login e persistência autenticada e anexar aprovação formal.

Nenhuma classe/projeto de teste e nenhum módulo de negócio foram criados.
