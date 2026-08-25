# Fechamento FUNC17

## Entrega
- 19 tabelas jurídicas idempotentes com identidades `bigint`, integridade, índices e 25 permissões RBAC.
- Application contracts, repositório Dapper transacional e registro de DI.
- Dashboard e rotas MVC/Razor operacionais, filtros, estados vazios, feedback, CSRF e navegação lateral.
- CSV para 13 recursos, com neutralização de fórmulas e auditoria da exportação.
- Referências documentais e integrações somente por metadados.

## Limites confirmados
InovaGED, GED, Protocolo e Tributário não foram alterados. Não existe upload, EF Core, segredo, tenant/entidade/usuário hardcoded, mock ou fallback de dados.

## Validação
Os resultados efetivamente executados e eventuais bloqueios de SDK/PostgreSQL devem constar na PR; ausência de infraestrutura é `BLOCKED`, nunca `PASS`.
