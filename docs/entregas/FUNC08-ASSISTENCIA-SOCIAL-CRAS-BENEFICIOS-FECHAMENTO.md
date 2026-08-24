# Fechamento FUNC08 — Assistência Social

## Evidências

- Schema corretivo idempotente com PK bigint identity, FKs, checks, índices e soft delete.
- Catálogo técnico de benefícios e RBAC persistidos, sem pessoas/famílias fictícias.
- Dashboard e operações existentes conectados à API Social; aliases MVC de Assistência Social completos.
- Dapper substitui respostas de dashboard e famílias simuladas por consultas PostgreSQL multi-tenant.
- Consolidados e checksum do manifesto sincronizados.

## Gates

- `dotnet restore` e `dotnet build`: **BLOCKED** neste ambiente porque o executável `dotnet` não está instalado.
- Execução PostgreSQL idempotente: **BLOCKED** enquanto não houver `psql` e uma instância configurada por `ConnectionStrings__DefaultConnection`.
- Smoke autenticado: **BLOCKED** pela ausência do runtime e de identidade/tenant homologado.
- RC50.68 permanece bloqueada; nenhuma release ou RC50.69 foi criada.
