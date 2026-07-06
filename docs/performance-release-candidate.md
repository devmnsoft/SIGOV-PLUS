# Performance mínima da Release Candidate

Páginas pesadas revisadas como critério: Dashboard, Minha Central, Busca, Relatórios, Auditoria, Protocolo, GED, Pessoas, RH, Contratos, SIAFIC, Obras e Patrimônio.

Regras: paginação/LIMIT em listagens, filtros obrigatórios para relatórios amplos, timeout e CancellationToken em consultas, evitar N+1, não carregar base inteira em memória, cache curto para catálogos e índices recomendados para chaves/filtros.
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
