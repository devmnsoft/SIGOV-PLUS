# Fechamento FUNC14

## Entrega
Implementação funcional integrada nas camadas Application, Infrastructure e Web, migration PostgreSQL idempotente, catálogo RBAC, menu, telas Razor e documentação. Fonte da verdade: PostgreSQL via Dapper. Os scripts consolidados e o manifest incluem a mesma migration.

## Validações previstas
- `dotnet build`
- testes existentes da solução
- parse do `manifest.json`, checksum e igualdade da migration nos consolidados
- execução com `psql -v ON_ERROR_STOP=1` quando PostgreSQL 16 estiver disponível
- inspeção de rotas/views e busca por referências proibidas.

## Integrações e pendências reais
A integração tributária é somente preparada por registro de referência e estado técnico: não foi identificado contrato estável para emissão/baixa automática e não há guia fake. GED, InovaGED e Protocolo não foram modificados. Parametrização ambiental requer configuração municipal e validação jurídica antes de produção; o software não declara conformidade legal completa.
