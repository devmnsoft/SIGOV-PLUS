# Fechamento FUNC18

Entrega persistente e integrada do módulo municipal de trânsito. Inclui 19 tabelas, 29 permissões, 21 endpoints/telas, navegação, validações de domínio, isolamento tenant/entidade, auditoria e oito famílias de CSV. Todos os relacionamentos dos formulários usam opções reais do banco; nenhum ID relacional é solicitado em campo de texto. Scripts consolidados e manifesto estão sincronizados. Consulte `docs/FUNC18-TRANSITO-MOBILIDADE-FISCALIZACAO.md` para inventário e comandos.

Pendências devem ser limitadas a validações bloqueadas por ferramentas/serviços ausentes no ambiente; não há fallback fictício.

## Comandos executados
- `dotnet restore sigov.sln && dotnet build sigov.sln --no-restore`: **BLOCKED**, o executável `dotnet` não existe no ambiente (`command not found`).
- `python3 -m json.tool database/postgres/migrations/manifest.json`: aprovado.
- checksum SHA-256 da migration comparado ao manifesto: aprovado.
- aplicação com `psql`: **BLOCKED**, o executável `psql` não existe no ambiente.
- inspeções `rg` de IDs visíveis, ValidationSummary, validações e formulários POST: aprovadas.
