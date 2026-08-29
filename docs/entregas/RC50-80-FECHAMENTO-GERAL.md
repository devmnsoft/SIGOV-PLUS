# Entrega RC50.80 — fechamento geral

## Resultado

A release ganhou um gate estático único e reproduzível para impedir divergência
entre manifest, migrations, compatibilidades e scripts completos, além de
detectar regressão de antiforgery nas views. A matriz funcional e os contratos
de segurança/LGPD foram consolidados em `docs/FECHAMENTO-GERAL-SIGOV-PLUS.md`.
Os POSTs Razor encontrados sem token foram corrigidos transversalmente, e os
checksums/baselines antes divergentes foram reconciliados sem editar migration.

## Evidências e comandos

| Verificação | Resultado no checkout |
| --- | --- |
| Base e remotes Git | branch local `work`; nenhum remote |
| Gate RC50.80 | Executado com Python 3 |
| Build .NET 10 | BLOCKED: executável `dotnet` ausente |
| PostgreSQL 16 / scripts | BLOCKED: executável `psql` ausente |
| Smoke de rotas | BLOCKED: runtime e banco indisponíveis |

## Checklist de homologação externa

1. instalar exatamente o SDK de `global.json` e executar `dotnet build`;
2. aplicar e reaplicar o script de desenvolvimento em PostgreSQL 16 temporário;
3. comparar schema por manifest e baseline;
4. iniciar Api e Web com segredos apenas no ambiente;
5. executar login, dashboard, listagens, create/edit/details, exportações,
   outbox, transparência e relatórios com perfis autorizados e negados;
6. registrar falha como FAIL/BLOCKED, sem inferir sucesso.
