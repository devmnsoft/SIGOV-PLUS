# Entrega EXP06 — Jurídico360

## Entregue

- Migration idempotente e baselines sincronizados, incluindo permissões e índices contextuais.
- Evolução do FUNC17 com execução fiscal/CDA, carteira, risco, tarefas, alertas, documentos, publicações e precatórios/RPV.
- Rotas MVC existentes ampliadas para os novos recursos, dashboard e CSV auditado com proteção contra injection.
- Catálogo de autorização e visual premium responsivo.

## Regras e limites

O servidor exige tenant/entidade, usa Dapper/Npgsql e auditoria transacional. Não há integração fictícia com tribunal, cartório, assinatura ou pagamento. Beneficiário e processo sigiloso exigem permissões próprias; integrações externas só podem avançar com adaptadores oficiais.

## Comandos e bloqueios

- `git status --short --branch`
- `git log --all --grep=Cidadão360`
- `dotnet build --no-restore`

`BLOCKED: comando git fetch origin não executado porque o checkout não possui remote origin configurado.`

`BLOCKED: comando dotnet build --no-restore não executado porque o executável dotnet não está instalado no ambiente.`

`BLOCKED: comando psql -v ON_ERROR_STOP=1 -f database/postgres/migrations/20260827120000_exp06_juridico360_integrado.sql não executado porque o cliente psql e uma conexão PostgreSQL 16 não estão disponíveis no ambiente.`
