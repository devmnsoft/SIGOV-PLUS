# RC50.55-PROD — relatório de limpeza P0 do Production Gate

Data: 2026-08-19. **Decisão: GATE BLOQUEADO por P0 ambiental.**

1. Foram encontrados 52 usos executáveis de projeção curinga.
2. Foram corrigidos 14 arquivos C#, a migration 024, o manifest e quatro scripts consolidados oficiais.
3. Consultas estáticas listam colunas; helpers dinâmicos usam allowlist de tabela/projeção; consultas schema-aware escapam cada identificador validado.
4. Antes/depois dos validadores: 49 avisos parciais, 126 de colunas e 7 conservadores de `coalesce`; todos retornaram 0.
5. P0 estático corrigido: todos os curingas; nenhum P0 de índice foi comprovado.
6. Remanescentes: 49/126 P1 aguardando apply real e 7 P2 imutáveis documentados.
7. Migration alterada: `024_assistencia_social_base.sql`, somente projeção da view.
8. Checksum normalizado SHA-256 atualizado no manifest: `cb4117e8390fbd7fe281d678178ae57943f803d30d4052a18c953f4d875aabba`.
9. `database/postgres/script_completo{,_dev}.sql`, `database/script_completo.sql` e `script_completop.sql` foram sincronizados.
10. Banco limpo: não executado; `psql` ausente (P0_ENVIRONMENTAL).
11. Banco parcial: não executado pela mesma limitação.
12. Build: não executado; `dotnet` ausente (P0_ENVIRONMENTAL).
13. API/health/liveness/Swagger: sem processo executável neste host; probes isolados confirmaram indisponibilidade, não aprovação.
14. Web: sem processo executável; páginas críticas não foram declaradas aprovadas.
15. Worker: build/runtime pendente pela ausência do SDK.
16. Rotas: análise estática passou, 605 rotas sem conflito direto.
17. Páginas críticas: probe retornou HTTP 000 porque API/Web não estavam em execução.
18. Endpoints 501: nenhum padrão essencial encontrado; a única ocorrência `501` é parte do SQLSTATE `42501` na tela diagnóstica.
19. Workflow `production-gate.yml`: simulado pelo smoke local; execução GitHub pendente.
20. `prod-gate-local.ps1`: não executável neste Linux sem PowerShell/.NET/PostgreSQL.
21. P0 restante: ambiente obrigatório e, por consequência, provas limpa/parcial/build/runtime.
22. Decisão: bloqueado de forma segura; nenhum SKIP obrigatório foi convertido em sucesso.
23. RC50.56: executar workflow/Windows com ferramentas, anexar applies e probes autenticados; corrigir qualquer P1 promovido por falha real.

Nenhum módulo, projeto/classe de teste, mock ou fixture foi criado.
