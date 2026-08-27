# EXP09 — Entrega SST360

Entrega vertical do SST360 com schema idempotente, permissões persistidas, indicadores reais, páginas MVC/Razor responsivas e fluxo real de ASO. A implementação reutiliza o RH oficial e oferece rotas para todos os subdomínios solicitados, sem dados demonstrativos ou fallback.

## Operação

Aplicar `20260827150000_exp09_sst360.sql` pelo manifest. Configurar `ConnectionStrings__DefaultConnection`, autenticar um usuário com tenant e entidade e conceder apenas as permissões SST necessárias. Sem adaptador eSocial real, eventos permanecem explicitamente pendentes de integração.
