# OpenAPI/Swagger — SIGOV PLUS

Swagger disponível em `/swagger` no serviço API. A API v1 usa envelope padrão: `success`, `message`, `data`, `errors` e `correlationId`.

## Autenticação
- Header `X-Api-Key`: API key da integração/tenant.
- Header `X-Tenant-Id`: tenant obrigatório para dados multi-tenant.
- JWT pode ser usado por clientes internos quando configurado.

## Paginação e filtros
Endpoints de listagem aceitam `page`, `pageSize`, `status`, `de` e `ate` quando aplicável. `pageSize` é limitado para reduzir abuso.

## Tags Swagger
Health, Auth, Protocolos, Documentos, Tarefas, Notificações, Fluxos, Mobile, Assinaturas, BI e Integrações.

## Erros, rate limit e LGPD
Erros não devem expor stacktrace. Rate limit deve ser por API key/IP quando disponível. Dados pessoais são mascarados por padrão, e documentos sigilosos não são retornados por endpoints públicos.

## Versionamento
A versão inicial fica sob `/api/v1`. Mudanças incompatíveis devem criar `/api/v2`.
