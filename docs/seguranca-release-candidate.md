# Segurança básica de produção — Release Candidate

Checklist: cookies seguros, antiforgery em POST MVC, headers mínimos, sessão com expiração, senha com hash, bloqueio/rate limit quando disponível, CORS restrito, sem stacktrace fora de Development, páginas de erro amigáveis, logs com correlationId e sem dados sensíveis.

Bloqueante: qualquer stacktrace público, segredo em repositório ou POST crítico sem antiforgery.
## Evidência desta execução
O ambiente de agente em 2026-07-06 não possui `dotnet` nem `docker`; por isso comandos finais foram tentados e classificados como limitação operacional, não como aprovação técnica. A validação deve ser repetida em runner/estação com SDK .NET e Docker.
