# Configuração por ambiente

Development pode usar PostgreSQL local e seeds explicitamente de desenvolvimento. Homologation replica TLS, proxy, CORS e topologia produtiva com credenciais próprias. Production usa `ASPNETCORE_ENVIRONMENT=Production`, artefato Release imutável e secrets externos.

Use `ConnectionStrings__DefaultConnection` (database `postgres`, `Search Path=sigov`), `Sigov__Security__CorsAllowedOrigins__0`, `Sigov__Security__SwaggerEnabledInProduction=false`, `Authentication__CookieHours`, `Workers__Outbox__Enabled` e URLs por ambiente. Os `appsettings.Production.example.json` são moldes sem credenciais; vault, secret de serviço ou variável protegida fornece senha, bootstrap token, API keys e SMTP.

Em produção, HTTPS termina no proxy/IIS com forwarded headers confiáveis; HSTS, cookie Secure/HttpOnly/SameSite e headers permanecem ativos. Swagger fica desligado por padrão. Restrinja CORS à origem HTTPS real. Monte storage/upload fora do diretório publicado, com limite, antivírus, backup e permissão mínima. Ative workers individualmente somente após banco/readiness e monitore suas filas. Logs estruturados vão para sink central com retenção e sem token, senha, documento ou connection string.
