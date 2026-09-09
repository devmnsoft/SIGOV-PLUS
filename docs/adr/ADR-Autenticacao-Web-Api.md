# ADR — Autenticação Web e API

Status: PROPOSTO; validação ponta a ponta bloqueada pelo gate PostgreSQL 16 da RC50.99.

## Contexto

Web e API possuem transportes diferentes, mas precisam compartilhar identidade, sessão revogável, tenant, entidade, exercício e autorização persistida. Claims volumosas de permissões e fallbacks silenciosos tornam revogação e isolamento inconsistentes.

## Decisão

- Web usa cookie autenticado compacto e contexto persistido; API usa bearer token conforme o contrato existente.
- Claims transportam identificadores mínimos, nunca o catálogo completo de permissões.
- O backend resolve permissões, perfil, tenant ativo, entidade e entitlement no banco em cada decisão sensível, com cache invalidável quando existente.
- Endpoint anônimo não recebe contexto privilegiado implícito. Endpoint protegido sem identidade/contexto válido responde de forma fail-closed.
- Logout, revogação, troca de contexto e suspensão invalidam o acesso efetivo; mensagens e logs não expõem documento, token ou segredo.

## Evidência pendente

Login por CPF, CNPJ e e-mail; `/MinhaCentral`; logout; revogação; API anônima/autenticada; módulo não contratado; tenant suspenso e isolamento entre dois tenants. Nenhum desses fluxos foi declarado concluído nesta execução.
