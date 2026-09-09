# ADR — Catálogo SaaS e entitlements

Status: PROPOSTO; decisão de implementação adiada pelo gate P0 da RC50.99.

## Contexto

O banco deve ser a autoridade para catálogo comercial, contratação por tenant, vigência, suspensão e permissões. Coleções hardcoded, catálogos demo e consultas diretas de controllers às tabelas de contratação criam decisões divergentes.

## Decisão

Quando P1 for liberado, o catálogo canônico será persistido em `modulo_saas`; a contratação e sua vigência serão persistidas em `tenant_modulo_contratado`. Um avaliador único de entitlement combinará autenticação, tenant ativo, contexto de entidade, contratação vigente e permissão efetiva. Controllers e menus consumirão esse avaliador, sem consultar diretamente `tenant_modulo_contratado`, `tenant_modulo` ou `modulo_saas` para decidir acesso.

Compatibilidade legada será forward-only, auditável e sem remoção de tabelas nesta sprint. Ausência de schema ou configuração falhará explicitamente.

## Consequências

- Catálogo não concede acesso por si só.
- Contratação não substitui permissão do usuário.
- Menu e API devem produzir a mesma decisão.
- A implementação e a homologação permanecem `AGUARDA_GATE`; este ADR não comprova runtime.
