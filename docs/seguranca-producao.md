# Segurança de produção

- Senhas e JWT secrets não ficam em appsettings versionado.
- Seed demo é bloqueado em Production.
- CORS wildcard é proibido em Production.
- Swagger em Production fica desligado por padrão.
- Repositories críticos filtram por `tenant_id`.
- Módulos e feature flags são validados no backend.
