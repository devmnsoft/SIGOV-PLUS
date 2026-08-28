# EXP23 — Energia360

Entrega do FUNC23 com migration idempotente `20260828100000`, catálogo RBAC, repositório Dapper parametrizado e telas MVC/Razor responsivas. O painel consolida consumo, custo, demanda, iluminação, geração, créditos, eficiência e emissões evitadas oficiais. CRUDs usam seleção de relacionamentos reais e bloqueiam contexto incompleto.

## Integrações

- Pessoa jurídica: concessionárias existentes.
- Unidade organizacional e contratos: seletores canônicos.
- Financeiro, Cidadão360, Ativos/Fiscaliza360 e Carbono360: referências reais; nenhuma resposta é fabricada quando não configurados.
- PostgreSQL: 19 estruturas Energia360 com identidade `bigint`, contexto obrigatório, checks e índices.

## Operação

A migration, manifest e quatro baselines estão sincronizados. A execução em ambiente exige .NET 10 e PostgreSQL 16+, com `ConnectionStrings__DefaultConnection` fornecida pelo ambiente.
