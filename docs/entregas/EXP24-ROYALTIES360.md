# EXP24 — Royalties360

**Status:** implementado para validação em ambiente com .NET 10 e PostgreSQL 16+.

## Entrega

- Camadas Application, Infrastructure e Web com contratos, repositório Dapper/Npgsql, autorização e Razor responsivo.
- Dezesseis agregados persistidos, chaves `bigint identity`, contexto institucional, checks de valores/vigência/visibilidade e índices operacionais.
- Dashboard previsto × realizado, repasses, aplicações, saldos, projetos integrados e alertas.
- Cadastro controlado com seletores provenientes do banco, validação no servidor, antiforgery e trilha de justificativas.
- Relatórios CSV protegidos contra injection e transparência limitada a registros publicáveis.

## Operação

Aplicar `20260828120000_exp24_royalties360.sql` pelo runner/manifest. Configurar somente `ConnectionStrings__DefaultConnection`. Integrações ANP/STN/Tesouro dependem de adaptador real; até lá, usar entrada manual controlada com fonte declarada.
