# Entrega RC50.88 — RH360, Folha e Portal

A entrega adiciona o modelo PostgreSQL idempotente para o ciclo funcional completo, índices de isolamento e unicidade, checks financeiros/previdenciários, permissões oficiais e áreas MVC para RH, folha, eSocial e Portal do Servidor.

## Critérios

- Multi-tenant e multi-esfera em todas as novas tabelas.
- Identidades `bigint`, Dapper/Npgsql e SQL parametrizado.
- CPF indexado somente por hash e auditoria de acesso sensível.
- Folha com competência e memória, sem dinheiro em ponto flutuante.
- Portal limitado à identidade autenticada.
- Integrações externas sem simulação.
- Scripts completos e manifesto sincronizados com a migration `20260901060000`.
