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

## Correções do fechamento
- Navegação e downloads convertidos para Tag Helpers; Relatórios usa sua action própria.
- Tokens antiforgery explícitos nos formulários jurídicos de escrita.
- Controles de escrita condicionados à política `*_MANAGE` e contexto ausente tratado com `Forbid`.
- Auditoria somente leitura limitada a metadados; histórico transacional conserva o estado funcional.

## Evidências de 25/08/2026
Checksum da migration (`52b30af5bac763cd6903de92c80740996407e873c2e7fd6a170880ffeac35116`) corresponde ao manifest; FUNC17 existe nos quatro scripts consolidados e o manifest é JSON válido. Build, migration real e smoke HTTP ficaram `BLOCKED`: faltam `dotnet`, `psql`/PostgreSQL e, consequentemente, aplicação executável. Não são declarados como PASS.
