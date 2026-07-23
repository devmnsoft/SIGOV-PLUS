# Matriz de jobs Pós-RC 27A.1

| Job obrigatório | Estado inicial run 292 | Ação nesta entrega |
|---|---|---|
| workflow-integrity | failure | Instala actionlint via Go, publica logs e resultado JSON. |
| release-context | success | Lê versão centralizada `1.0.0-rc27a1`. |
| build-test | skipped | Desbloqueado após workflow-integrity. |
| dependency-injection | skipped | Mantido como gate com TRX validado. |
| web-smoke | skipped | Corrigido para DI real e CSS por HTTP. |
| powershell-lint | skipped | Mantido com artifacts always. |
| migrations-manifest | skipped | Mantido com PostgreSQL 16 e migration.log. |
| seed-idempotency | skipped | Mantido com duas execuções e diff de contagens. |
| script-completop-validate | skipped | Mantido sem edição manual do SQL consolidado. |
| script-completop-idempotency | skipped | Mantido com duas execuções. |
| schema-equivalence | skipped | Mantido com artifacts de diff. |
| standalone-postgres-runtime | skipped | Mantido para API, Web e Worker sem Docker. |
| docker-build | skipped | Mantido para API, Web e Worker. |
| docker-compose-e2e | skipped | Mantido para compose healthy. |
| ui-contrast | skipped | Mantido. |
| release-package-check | skipped | Mantido. |
| go-live-check | skipped | Depende de todos os gates obrigatórios. |
