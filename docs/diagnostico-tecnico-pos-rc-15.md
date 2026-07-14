# Diagnóstico técnico Pós-RC 15

Gerado em 2026-07-14T16:48:52.156390Z.

- Último commit local da main/worktree analisado: `64028aed452b4c6ae4bfd35d9f8d8bc2730de41f Merge pull request #114 from devmnsoft/codex/corrigir-bugs-e-validar-runtime-no-sigov-plus`.
- Último PR mergeado informado/analisado: PR #114.
- Escopo: workflows, scripts, controllers, views, serviço Dapper, migrations, seeds e módulos operacionais solicitados.

| Área | Arquivo | Situação atual | Bug/Risco | Correção obrigatória | Status |
|---|---|---|---|---|---|
| CI/CD | .github/workflows/ci.yml | Possui workflow_dispatch, push main, pull_request e jobs build-test/docker-build/sql-validate/smoke-static/docker-compose-e2e/release-package-check/go-live-check. | Referências Pós-RC 14 poderiam gerar artefato obsoleto. | Atualizar artifacts/evidências para Pós-RC 15 e manter upload obrigatório. | Corrigido |
| CI/CD | .github/workflows/release.yml | Release manual com build, test, Docker opcional, validate-release, package-release e go-live. | Sem evidências Pós-RC 15 se scripts empacotarem docs antigas. | Atualizar scripts para docs Pós-RC 15. | Corrigido |
| Runtime | scripts/smoke-test-sigov.ps1 | Smoke cobre rotas Web, health API, API v1 com/sem chave e endpoints Enterprise. | Ambiente local sem runtime impede comprovação operacional neste workspace. | Gerar evidências Pós-RC 15 e manter falha bloqueante quando rotas críticas falham. | Corrigido/pendente ambiente |
| CI/CD | scripts/go-live-check.ps1 | Valida docs, smoke, workflow, release package e ausência de .env real. | Checklist apontava docs Pós-RC 14. | Atualizar matriz documental para Pós-RC 15. | Corrigido |
| CI/CD | scripts/package-release.ps1 | Sanitiza .env.example, bloqueia segredos e empacota migrations/seeds/scripts. | Pacote podia omitir docs Pós-RC 15. | Incluir documentação/evidências Pós-RC 15. | Corrigido |
| Funcional | src/Sigov.Api/Controllers/EnterpriseModulesController.cs | Endpoints de CRUD, import-template/preview/confirm, batch e anexos existem com tenant/permissão. | Anexos ainda dependem de GED/storage e retornam fallback/503 honesto. | Manter fallback honesto sem falso sucesso e sem expor storage path. | Parcial honesto |
| Funcional | src/Sigov.Web/Controllers/OperationalTransversalController.cs | Kanban autenticado, POST antiforgery, auditoria e leitura via IEnterpriseCrudService para OS/Propostas. | Fallback demo de tenant não pode valer em produção. | Restringir tenant demo ao ambiente não produção; produção usa tenant real/Guid.Empty sem simular. | Corrigido |
| UX | src/Sigov.Web/Views/Kanban/Index.cshtml | Cards exibem fonte, botão de detalhe e POST de mudança de status. | Sem cards reais quando schema ausente. | Exibir fonte/fallback claramente. | Atendido |
| Funcional | src/Sigov.Web/Views/Enterprise/ModulePage.cshtml | Página Enterprise integrada ao JS CRUD/importação/lote/anexos. | Depende de schema e API para fluxo real. | Usar mensagens claras de parcial/falha. | Monitorado |
| Funcional | src/Sigov.Web/wwwroot/js/enterprise-crud.js | CRUD Enterprise aciona APIs existentes. | Risco de falso verde se resposta 207/409 não for exibida. | Manter relatório por item e mensagem de falhas. | Monitorado |
| Funcional | src/Sigov.Web/wwwroot/js/enterprise-form-metadata.js | Metadados de formulários centralizados. | Campos obrigatórios divergentes da importação. | Sincronizar validações com preview CSV. | Monitorado |
| Runtime | src/Sigov.Infrastructure/EnterpriseDapperCrudService.cs | Dapper CRUD operacional com schema quando existe e status SCHEMA_UNAVAILABLE quando ausente. | Runtime depende de PostgreSQL/migrations. | Validar em Docker E2E. | Pendente ambiente |
| Runtime | database/postgres/migrations | Migrations PostgreSQL versionadas. | Ordem precisa ser comprovada em banco real. | CI sql-validate e Docker E2E aplicam em ordem. | Pendente ambiente |
| Runtime | database/postgres/seeds/pos_rc_homologacao_demo.sql | Seed demo idempotente validado por CI. | Workspace atual não executou psql. | Aplicar duas vezes em Docker/CI. | Pendente ambiente |
| Funcional | Dashboard | Controller/serviço existentes para cards reais/fallback. | Cards dependem de tabelas e permissões. | Registrar fonte/período/drill-down. | Monitorado |
| Funcional | Minha Central | Controller/serviço existentes para visão do usuário. | Dependência de tabelas reais para contadores. | Exibir fonte e links filtrados. | Monitorado |
| Funcional | Busca | Busca global existente via PostBuildSaasService. | Cobertura Enterprise depende de catálogo/tabelas. | Manter LGPD/tenant. | Monitorado |
| LGPD | Relatórios | CSV com auditoria e mascaramento em controladores dedicados. | Risco de fórmula CSV. | Neutralizar campos e usar BOM UTF-8. | Monitorado |
| Auditoria | Notificações/Tarefas/Outbox/Auditoria | Módulos e migrations existem; eventos são referenciados em importação/lote/anexos/Kanban. | Serviço único de evento pode ser evolução futura. | Documentar pendência honesta. | Pendente evolução |
| Funcional | Agenda/SLA | Rotas Agenda e filtros de prazo/SLA existem em OperationalTransversal. | SLA real depende de fontes. | Mostrar fallback honesto quando fonte ausente. | Monitorado |
| Segurança | GED/Protocolo | Rotas e controllers existentes; anexos Enterprise não expõem storage path. | Provider indisponível deve retornar 503, não sucesso falso. | Mantido fallback 503. | Atendido |

## Conclusão honesta

O workspace atual não possui `dotnet`, portanto build/test runtime local ficaram bloqueados por limitação de ambiente e foram registrados nas evidências. As correções aplicadas removem referências Pós-RC 14 dos artefatos de CI/release/smoke, fortalecem o tenant do Kanban para não usar fallback demo em produção e documentam pendências que só podem ser fechadas com Docker/PostgreSQL/GED em execução.
