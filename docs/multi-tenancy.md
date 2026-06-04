# Multi-tenancy

Resolução de tenant ocorre por subdomínio/domínio, header `X-Sigov-Tenant` somente em Development/Test, query string somente em Development/Test e claim autenticada `tenant_slug`.

Em Production, tenant informado pelo frontend não é autoridade. O token/cookie precisa coincidir com o tenant resolvido. Tenants suspensos ou cancelados são bloqueados antes de operações comuns.
