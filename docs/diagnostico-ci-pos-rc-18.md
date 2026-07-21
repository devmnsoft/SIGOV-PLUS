# Diagnóstico CI Pós-RC 18

Não foi possível consultar integralmente logs remotos do GitHub Actions a partir deste ambiente. As causas tratadas localmente foram reproduzidas por inspeção estática:

| Job | Causa raiz | Correção |
|---|---|---|
| build-test | `TarefaService` duplicado e registro DI ambíguo; `ITarefaService` herdava contrato de persistência. | Orquestração mantida em Application, DI qualificado e contrato explícito. |
| sql-validate | Migration Agro usava `ON CONFLICT (chave)` sem garantia de índice único. | Seed reescrito com update por chave e insert com `WHERE NOT EXISTS`. |
| release-package-check | Scanner podia acusar variável/valor demo controlado no próprio smoke. | Allowlist restrita para smoke demo e fonte do scanner, mantendo bloqueios sensíveis. |
