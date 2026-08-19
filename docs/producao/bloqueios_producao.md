# Bloqueios de produção

**P0 (bloqueia):** migration/build/API/Web/login falha; senha dev ou connection string exposta; Swagger público; PII crítica sem máscara; exportação sem auditoria; rota crítica 500/menu 404; worker crítico falha sem log. Todo P0 deve estar encerrado com evidência.

**P1 (aprovação excepcional):** dashboard vazio sem explicação, relatório essencial ausente, enforcement parcial, observabilidade ou rollback incompletos. Exige responsável, prazo e mitigação.

**P2 (backlog):** polimento visual, performance secundária e integrações preparatórias. Não mascarar P0/P1 como P2.

## Estado RC50.53-PROD — 2026-08-19

**Decisão: não apto.** O ambiente de validação não possui .NET SDK, `psql`, `pg_dump` ou `pg_restore`.

- **P0 ambiental aberto:** instalar as ferramentas e tornar PostgreSQL `localhost:5432` acessível.
- **P0 sem evidência:** aplicação limpa/parcial, build, API/Web, logins, health/Swagger/páginas críticas e backup/restore.
- **P1 sem evidência:** Worker e persistência ponta a ponta de permissões, LGPD, auditoria e exportações.
- **P1 técnico:** revisar com execução PostgreSQL os avisos conservadores históricos dos validadores de índices.
- **P2:** CSP sem `unsafe-inline`, tuning medido e polimento visual.

Os gates estáticos passam, mas não encerram nenhum P0 runtime. O relatório detalhado está em `docs/execucao/rc50_53_prod_relatorio_validacao_integrada_real.md`.
