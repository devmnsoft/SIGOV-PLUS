# Segurança e LGPD Enterprise Pós-RC 10

## Autenticação e autorização

A API Enterprise exige `[Authorize]`, tenant resolvido e permissão por ação. `ADMIN_GERAL` e `ADMIN_TENANT` mantêm bypass administrativo conforme escopo do tenant. Operações sem permissão retornam 403.

## Tenant real

Produção sem `X-Tenant-Id`/claim/contexto válido é bloqueada. Fallback demo só é permitido fora de produção e com `Enterprise:AllowDemoTenantFallback` habilitado explicitamente, com warning em log.

## Auditoria

`EnterpriseExecutionContext` propaga tenant, usuário, login, IP, user agent, correlationId e permissões para o serviço Dapper. Campos `created_by`, `updated_by`, `deleted_by` e eventos operacionais usam o ator real quando disponível.

## CSV seguro

Exportações aplicam tenant/permissão, usam dados já mascarados, removem quebras de linha, substituem separador `;` e prefixam células que poderiam ser interpretadas como fórmula.

## Fallback honesto

Ações críticas com schema indisponível retornam `SCHEMA_UNAVAILABLE` e HTTP 503/424 equivalente, sem mensagens de falso sucesso.
