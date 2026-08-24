# Fechamento FUNC07 — Saneamento Comercial e Operacional

## Entrega

A entrega reaproveita a implementação Dapper/Npgsql e os endpoints RC50.50, acrescenta o schema comercial normalizado, regras operacionais, índices, auditoria e o conjunto RBAC solicitado. As rotas canônicas foram conectadas ao workspace Razor que consulta exclusivamente dados reais.

## Segurança e integrações

Autenticação e tenant são obrigatórios nos controllers. Almoxarifado e Frotas são referenciados apenas por identificadores opcionais, sem baixa ou movimentação implícita. O atendimento é interno ao domínio de saneamento e não altera InovaGED/Protocolo.

## Release

Esta entrega não promove RC50.68, que permanece bloqueada pelos gates externos, e não inicia RC50.69. A promoção depende de CI, runtime .NET 10 e PostgreSQL 16 disponíveis e aprovados.
