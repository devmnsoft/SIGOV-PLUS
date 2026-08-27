# EXP08 — Entrega Ativos360

## Entregue

- Dashboard premium, responsivo e alimentado por dados persistidos.
- Hub único para almoxarifado, estoque, requisições, patrimônio, inventários e frotas.
- Migration idempotente `20260827090000` com checks não negativos, índices, FKs condicionais e permissões.
- Scripts completos e manifesto sincronizados.
- Documentação funcional e de operação.

## Decisões

As tabelas oficiais `sigov.almoxarifado_*`, `sigov.patrimonio_*` e `sigov.frota_*` permanecem como autoridade. Views de integração dão nomes Ativos360 às requisições e inventários existentes, sem copiar dados. Tabelas novas existem somente para capacidades complementares.

## Validação

Executar `dotnet build`, validação do manifesto/checksums, parser SQL PostgreSQL, varreduras Razor e smoke de rotas. A aplicação exige `ConnectionStrings__DefaultConnection` e contexto autenticado para smoke dinâmico.
