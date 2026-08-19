# Gate de produção SIGOV+

A aprovação é formal e exige evidência anexada ao change. Item não comprovado permanece **não atendido**.

## Banco
- [ ] `script_completo_dev.sql` e `script_completo.sql` aplicam do zero em `postgres`, schema/search path `sigov`.
- [ ] instalação parcialmente migrada converge; `schema_migrations` contém manifest/checksums; nenhuma migration, coluna, constraint ou índice falha.
- [ ] índices passam validadores de coluna, índice parcial e imutabilidade; mudanças destrutivas possuem proteção e aprovação.
- [ ] backup pré-migration existe e restore foi ensaiado em banco separado.

## Build e runtime
- [ ] Domain, Shared (quando existir), Application, Infrastructure, Api, Web e Worker compilam Release com warnings como erros.
- [ ] API, Web e Worker sobem; Swagger somente no ambiente permitido; health/liveness respondem.
- [ ] admin e superadmin entram, MinhaCentral e ProjectStatus abrem; páginas críticas não retornam 404/500.

## Segurança
- [ ] secrets são injetados externamente; não há senha de desenvolvimento em Production.
- [ ] Swagger está desligado/protegido; CORS é allowlist; cookies são Secure/HttpOnly/SameSite; HTTPS/HSTS e headers estão ativos.
- [ ] API e menus aplicam permissões; dados pessoais são mascarados; exportações e negativas são auditadas.

## Operação
- [ ] logs estruturados têm correlation id sem segredo/PII; health e migrations têm diagnóstico sanitizado.
- [ ] backup, restore, rollback, deploy, workers e troubleshooting estão documentados.
- [ ] smoke de produção-like e checklist pós-deploy foram arquivados; nenhum P0 está aberto.

## Automação RC50.54

Dispare **SIGOV+ Production Gate** em `workflow_dispatch` ou por PR para `main`. Os jobs `static-validation`, `database-clean-apply`, `database-partial-apply`, `runtime-build`, `runtime-smoke` e `artifact-summary` precisam terminar em sucesso. Baixe `rc50-54-production-evidence`; `SKIP` de ferramenta/banco/build, segredo ou P0 torna o gate reprovado. No Windows execute `scripts/prod-gate-local.ps1` conforme `prod_gate_local_windows.md`.

## RC50.55 — projeções e severidade

O gate estático deve exigir zero correspondências executáveis para projeções curingas nos diretórios protegidos. Saída warning dos validadores permanece artifact: exit code não zero ou falha PostgreSQL é P0; aviso conservador aprovado apenas após applies limpo/parcial é P1; casamento imutável comprovado pode ser P2 documentado. Ausência de ferramenta obrigatória continua bloqueando, nunca aprovando por `SKIP`.
