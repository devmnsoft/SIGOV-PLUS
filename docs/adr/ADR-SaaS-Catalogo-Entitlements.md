# ADR — Catálogo SaaS e entitlements

Status: PROPOSTO; implementação RC51.02 no código; ACEITO somente após testes runtime PostgreSQL 16 verdes.

## Contexto

O banco deve ser a autoridade para catálogo comercial, contratação por tenant, vigência, suspensão e permissões. Coleções hardcoded, catálogos demo e consultas diretas de controllers às tabelas de contratação criam decisões divergentes.

## Decisão

Quando P1 for liberado, o catálogo canônico será persistido em `modulo_saas`; a contratação e sua vigência serão persistidas em `tenant_modulo_contratado`. Um avaliador único de entitlement combinará autenticação, tenant ativo, contexto de entidade, contratação vigente e permissão efetiva. Controllers e menus consumirão esse avaliador, sem consultar diretamente `tenant_modulo_contratado`, `tenant_modulo` ou `modulo_saas` para decidir acesso.

Compatibilidade legada será forward-only, auditável e sem remoção de tabelas nesta sprint. Ausência de schema ou configuração falhará explicitamente.

O aplicador operacional deve recusar versões e checksums de ledger ausentes do manifesto. Um checksum histórico somente é compatível quando consta em `knownChecksums` e a migration possui pós-condição específica, reavaliada contra o estado final; configuração comercial mutável não integra essa invariável estrutural.

## Consequências

- Catálogo não concede acesso por si só.
- Contratação não substitui permissão do usuário.
- Menu e API devem produzir a mesma decisão.
- A implementação de catálogo persistido, `IModuleEntitlementEvaluator` e SuperAdmin de contratação entrou na RC51.02. Homologação permanece `AGUARDA_GATE` até PostgreSQL 16 runtime.
