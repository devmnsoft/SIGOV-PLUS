# API pública/privada SIGOV PLUS

Base proposta: `/api/v1`. Recursos: tenants, usuarios, protocolos, documentos, contratos, juridico, tributario, financeiro, workflow, notificacoes e health.

Regras: versionamento por URL, autenticação por JWT/API key quando aplicável, paginação (`page`, `pageSize` até 100), filtros explícitos, respostas com `correlationId`, auditoria de endpoints sensíveis, rate limit em login/API keys/exportações e mascaramento LGPD.

Nesta sprint, `/api/v1/health` e `/api/v1/{resource}` foram preparados como contrato inicial. Dados reais dependem de schema, autenticação e política de exposição por tenant.
