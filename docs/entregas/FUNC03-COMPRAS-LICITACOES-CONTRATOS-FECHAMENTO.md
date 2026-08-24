# Fechamento — FUNC03 Compras, Licitações, Contratos e Atas

## Escopo fechado
Foram entregues schema idempotente, Dapper/Npgsql, API e MVC com autorização persistente para fornecedores, solicitações, processos/fases, preços, julgamento/homologação, contratos e atas. Recebimentos possuem vínculos rastreáveis com FUNC02 e pendência para FUNC01, sem tombamento automático incompleto.

## Validações
- `git diff --check`, JSON e checksums do manifest: executados no fechamento.
- `dotnet restore` e `dotnet build sigov.runtime.slnf`: executados quando o SDK esteve disponível; resultado registrado no PR.
- `psql -v ON_ERROR_STOP=1`: **BLOCKED** quando não houver PostgreSQL 16 seguro configurado por `ConnectionStrings__DefaultConnection`; nenhum PASS é inferido.
- Smoke HTTP autenticado: **BLOCKED** sem runtime, banco e identidade/contexto oficiais; presença das rotas é verificada programaticamente.

RC50.68 continua **BLOCKED**. RC50.69 não foi iniciada. Nenhuma release foi promovida.
