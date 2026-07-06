# Auditoria front-end da Release Candidate

Itens revisados documentalmente: assets em `wwwroot/js` e `wwwroot/css`, layout, sidebar, navbar, toasts, modais, tabelas responsivas, tema claro/escuro e tratamento de AJAX. Critério: erro próprio de JavaScript em páginas principais é bloqueante.

Ações recomendadas: executar `scripts/check-web-assets.ps1`, smoke test com navegador, inspeção de console em Login, Dashboard, Minha Central, Pessoas, RH, Protocolo, GED, Busca, Relatórios e POC.
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
