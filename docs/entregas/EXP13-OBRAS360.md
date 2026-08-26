# Entrega EXP13 — Obras360

## Principais entregas

- Dashboard operacional com execução físico-financeira, pendências de campo, conformidade e transparência.
- Rotas MVC/Razor para cronogramas, diários, medições, aditivos, reajustes, reequilíbrios, ocorrências, não conformidades, ordens, evidências, transparência e relatórios.
- Formulários reais com seleção de obra, validação, antiforgery e auditoria.
- Schema idempotente e baseline sincronizada; permissões persistidas no banco.
- CSV seguro contra injection e integração rastreável sem mocks.

## Validação planejada

`dotnet build`, política SQL, JSON/checksums do manifest, equivalência dos scripts, auditorias estáticas dos formulários e smoke das rotas. A aplicação integral no PostgreSQL exige `ConnectionStrings__DefaultConnection` e servidor PostgreSQL 16 disponível.

## Bloqueios externos

Upload binário e entrega a sistemas externos (Financeiro, GED e portal) dependem dos adaptadores oficiais e não são simulados.
