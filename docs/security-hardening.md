# Hardening de segurança

## Production startup

O validador de `SigovOptions` falha em Production quando:

- `Sigov:Jwt:Secret` está ausente ou tem menos de 32 caracteres.
- `Sigov:Security:CorsAllowedOrigins` está vazio ou contém `*`.
- `Sigov:Seed:Demo` está habilitado.
- Swagger Production é habilitado sem token bootstrap de proteção.

## Headers e rate limit

A API aplica headers de segurança em middleware dedicado e rate limit simples por tenant/IP, com exceção para `/api/health`.

## Logs e erros

Health checks registram erros internamente e retornam respostas sem stack trace para o usuário final.
