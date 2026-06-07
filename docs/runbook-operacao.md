# Runbook de operação

## Incidente em health ready

1. Verifique `/api/health/db`.
2. Verifique `/api/health/outbox`.
3. Consulte logs por `CorrelationId`.
4. Valide conectividade com PostgreSQL e existência do schema `sigov`.

## Worker/Outbox

O Worker chama `IOutboxJob`; o job usa processor, repository, handlers e retry policy separados. Falhas em eventos individuais não derrubam o Worker e são marcadas para retry/dead-letter.

## Go-live

Execute `scripts/go-live-check.ps1` e depois `scripts/validate.ps1` antes de promover release.
