# Fechamento FUNC04 — Frotas

## Entrega

Foram fechados schema, contratos Application, serviço Dapper/Npgsql, autorização persistente, API, telas Razor administrativas, auditoria antes/depois e proteção LGPD. Migration, manifest e quatro scripts consolidados estão sincronizados. As integrações preservam os contratos de FUNC01/FUNC02/FUNC03: vínculo patrimonial opcional, instrumentos/fornecedores referenciados e saída de estoque atômica somente para item e local informados com saldo suficiente.

## Validações locais em 2026-08-24

- `git diff --check`: executado no fechamento.
- `python3 -m json.tool database/postgres/migrations/manifest.json`: executado no fechamento.
- checksum SHA-256 do manifest: recalculado e comparado no fechamento.
- `dotnet restore sigov.runtime.slnf` e `dotnet build sigov.runtime.slnf`: **BLOCKED**, SDK .NET 10 ausente no contêiner.
- `psql -v ON_ERROR_STOP=1 ...`: **BLOCKED**, cliente/instância PostgreSQL 16 segura e `ConnectionStrings__DefaultConnection` não disponíveis.
- smoke manual autenticado das rotas MVC: **BLOCKED**, runtime e banco não disponíveis.

Nenhum PASS foi inferido para comando bloqueado. RC50.68 continua **BLOCKED** e RC50.69 não foi iniciada. Não houve promoção de release.
