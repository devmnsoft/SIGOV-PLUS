# sigov

Plataforma SaaS de gestão pública municipal para operação real de prefeituras, câmaras, autarquias, fundos, secretarias e unidades descentralizadas.

## Stack

ASP.NET Core, C# 10, Clean Architecture, DDD, Dapper, PostgreSQL, API REST, Bootstrap 5, JavaScript puro, Serilog/ILogger, Docker e testes automatizados.


## Rodando com Docker

O ambiente Docker local sobe PostgreSQL 16, migrations automáticas, API, Worker configurável, Web MVC/Razor e storage persistente sem exigir PostgreSQL instalado na máquina.

```powershell
copy .env.example .env
docker compose up -d --build
```

- Web: http://localhost:8080
- API: http://localhost:5001
- Banco local: `localhost:5432`, database/user/senha conforme `.env`.
- Migrations manuais: `scripts/docker-apply-migrations.ps1`.
- Logs: `scripts/docker-logs.ps1`.
- PSQL: `scripts/docker-psql.ps1`.
- Reset total com confirmação: `scripts/docker-reset.ps1`.

OCR/preview e workers opcionais ficam desabilitados por padrão no Docker local e podem ser habilitados por variáveis no `.env`. Consulte o guia completo em [`docs/docker-local.md`](docs/docker-local.md).

## Execução local

- Web: http://localhost:5000
- API: http://localhost:5001
- Swagger Development: http://localhost:5001/swagger
- Health live: http://localhost:5001/api/health/live
- Health ready: http://localhost:5001/api/health/ready
- DB Health: http://localhost:5001/api/health/db

```powershell
scripts/start-dev.ps1
```

## Banco de dados

O PostgreSQL usa o database `sigov`, usuário `sigov` e schema físico único `sigov`. Multi-tenancy usa banco e schema compartilhados com `tenant_id` obrigatório nas tabelas operacionais, filtros na aplicação e Row-Level Security preparado para tabelas críticas.

## SaaS production-ready

A camada SaaS inclui tenants, domínios, planos, assinaturas, módulos contratados, feature flags, limites, uso mensal, eventos operacionais, health checks, Docker Production, scripts de backup/restore e CI/CD.

O antigo conteúdo de conformidade/aderência fica tratado como módulo acessório administrativo, sem posicionar esse conteúdo como núcleo do produto.

## Etapas implementadas

- Etapa 1: estrutura Clean Architecture, Docker Compose, PostgreSQL `sigov`, migrações, SaaS/multi-tenancy e módulos base.
- Etapa 2: módulo Pessoa e Endereço com API REST, Dapper, auditoria LGPD, UI CSHTML/jQuery/Ajax e exportação CSV/JSON/XML. Consulte `docs/etapas/02-pessoas-enderecos.md`.

- Etapa RH: módulo Recursos Humanos com cadastros de servidores/cargos/lotações/vínculos, folha inicial, ponto/frequência, férias/afastamentos, saúde ocupacional, eSocial estrutural, portal do servidor, dashboards, exportação CSV/JSON, auditoria JSONB, LGPD, outbox e integração preparada com Financeiro/SIAFIC.

Etapa concluída: Recursos Humanos – Próxima etapa: Gestão de Patrimônio/Inventário/Obras (integração RH e Financeiro).

## Release Candidate 1.0.0-rc.2

Esta versão candidata congela o escopo do SIGOV PLUS para homologação técnica/comercial sem abrir módulos novos. A matriz oficial de status está em [`docs/matriz-modulos-release-candidate.md`](docs/matriz-modulos-release-candidate.md) e o escopo está em [`docs/release-candidate-escopo.md`](docs/release-candidate-escopo.md).

Comandos esperados para validação em ambiente com .NET SDK e Docker:

```powershell
dotnet clean
dotnet restore
dotnet build
dotnet test
docker compose up -d --build
scripts/smoke-test-sigov.ps1
```

Módulos parciais, demonstrativos ou em implantação não devem ser apresentados como funcionalidades integrais de produção. O fallback honesto permanece obrigatório.

## Pós-RC: homologação real da API v1 e fluxo operacional

A sprint Pós-RC adiciona uma base homologável para API key real, webhooks, outbox e persistência de Protocolo + GED + Workflow. A migration `20260706153000_pos_rc_protocolo_ged_workflow_api_outbox.sql` é idempotente e não destrutiva, sempre com `tenant_id`, auditoria, LGPD, soft delete, `correlation_id` e índices operacionais. Rotas `/api/v1/*` passam a exigir `X-Api-Key` e `X-Tenant-Id`, com validação por hash e escopos; health permanece público.

Fallback honesto permanece obrigatório: provedores oficiais de assinatura/OCR/storage externo e integrações sem configuração real não devem ser simulados.

## Pós-RC 02 — persistência real operacional

- Funcional real: API v1 com API key/tenant/escopos, Protocolo e GED persistindo nas tabelas Pós-RC, Outbox worker consumindo sigov.outbox_evento.
- Parcial: telas MVC administrativas continuam com fallback honesto quando ação/formulário não possui todos os dados reais.
- Dependente de provedor: OCR, ICP/Gov.br e entrega externa oficial de webhooks.
- LGPD: respostas e logs não devem expor dados pessoais completos nem token claro.


## Pós-RC 03 — homologação Web real

- **Funcional real:** Protocolo e GED Web passam a acionar serviços Dapper para `sigov.protocolo`, `sigov.protocolo_movimento`, `sigov.workflow_instancia`, `sigov.tarefa`, `sigov.notificacao`, `sigov.documento`, `sigov.documento_versao`, `sigov.protocolo_anexo`, `sigov.portal_validacao_documento` e `sigov.outbox_evento` quando o schema existe.
- **Parcial:** Dashboard, Minha Central, Busca e Relatórios mantêm fallback honesto e devem priorizar dados reais detectados no schema.
- **Em implantação/fallback:** PDF/DOCX da POC, OCR, ICP-Brasil e Gov.br não são simulados.
- **Dependente de provedor:** envio externo de webhook e validações oficiais dependem de infraestrutura configurada.
- **Não disponível:** exposição de path físico de storage e dados pessoais completos em listagens/exports.

## Pós-RC 04 — homologação final e dados demonstráveis

Para preparar a demonstração/homologação técnica:

```powershell
docker compose up -d
pwsh -NoProfile -File scripts/apply-demo-seed.ps1
pwsh -NoProfile -File scripts/smoke-test-sigov.ps1
```

O seed `database/postgres/seeds/pos_rc_homologacao_demo.sql` cria tenant, usuários, permissões, protocolos, documentos GED, workflows, tarefas, notificações, outbox, webhook inativo e API key demo apenas com hash. Os dados são fictícios e seguros para apresentação. Consulte `docs/guia-homologacao-comercial.md`, `docs/roteiro-demo-sigov-plus.md` e `docs/checklist-go-live-pos-rc.md`.

## Pós-RC 05 — hardening CI/CD e pacote Go-Live

Esta versão adiciona consolidação de CI/CD no GitHub Actions, smoke E2E Web/API, validação SQL em PostgreSQL, empacotamento de release e documentação de homologação final.

### Validação recomendada

```bash
dotnet restore sigov.sln
dotnet build sigov.sln --configuration Release
dotnet test sigov.sln --configuration Release
docker compose build --no-cache
docker compose up -d
```

Depois, aplicar o seed demo e executar `scripts/smoke-test-sigov.ps1`. Em ambientes sem `pwsh`, aplicar `database/postgres/seeds/pos_rc_homologacao_demo.sql` via `psql` e executar o smoke em host com PowerShell 7.

### Limitação honesta

No container do agente da sprint Pós-RC 05, `dotnet`, `docker` e `pwsh` não estavam instalados; por isso, as evidências finais de build, testes, Docker e smoke devem ser obtidas no GitHub Actions e no ambiente de homologação.

## Pós-RC 06 — CI real, smoke autenticado e Go-Live

A Pós-RC 06 corrige a validação do schema para `sigov.outbox_evento`, adiciona schema report compatível com Docker local e PostgreSQL service do CI, seed demo com API key compatível com o middleware, smoke autenticado mascarado, job Docker Compose E2E e package release sanitizado.

Chave demo **somente local/homologação**: `sigov_demo_local_only_2026_please_rotate`. Rotacione antes de qualquer uso real. O banco armazena apenas o hash SHA-256 hexadecimal.

Documentação operacional: `docs/ci-cd-pos-rc-06.md`, `docs/smoke-e2e-pos-rc-06.md`, `docs/release-package-pos-rc-06.md` e `docs/diagnostico-pos-rc-06.md`.


## Pós-RC 07

Homologação real multi-tenant e Go-Live controlado: ambiente local padronizado para banco `sigov`, tenant resolvido por contexto, dashboard com fonte Real/Demo/Fallback, CI com Docker Compose E2E, package release sanitizado e go-live-check executável.

## Pós-RC 07 — Enterprise funcional

O bloco Enterprise agora possui migration PostgreSQL idempotente, serviço Dapper com fallback honesto, telas Razor operáveis, CSV com LGPD, seed demo fictício e rotas de smoke para Comercial, OS, Estoque/Compras, Industrial e Indústria Produção. Consulte `docs/enterprise-funcional-pos-rc-07.md` e `docs/diagnostico-enterprise-pos-rc-07.md`.

### Enterprise Pós-RC 08
A rodada Pós-RC 08 consolidou a validação Enterprise ponta a ponta com CRUD REST, template MVC/Razor operacional, smoke ampliado e documentação de homologação. Consulte `docs/diagnostico-enterprise-pos-rc-08.md`, `docs/enterprise-pos-rc-08-validacao-e2e.md`, `docs/jornadas-enterprise-pos-rc-08.md` e `docs/matriz-crud-enterprise-pos-rc-08.md`.


## Pós-RC 09 — QA funcional Enterprise

- Diagnóstico criado em `docs/diagnostico-enterprise-pos-rc-09.md`.
- Evidências de homologação registradas em `docs/evidencias-enterprise-pos-rc-09.md` e `docs/evidencias-enterprise-pos-rc-09.json`.
- Manual de usuário e checklist QA criados para a jornada Enterprise navegável.
- UX Enterprise refinada com filtros, paginação, loading, detalhes, edição, inativação, restauração, CSV com tenant, toasts e fallback honesto.

## Pós-RC 10 — Hardening Enterprise

A rodada Pós-RC 10 endurece a operação Enterprise com autenticação obrigatória na API, tenant real obrigatório, permissões por ação, auditoria com contexto de usuário, fallback honesto para schema indisponível, formulários por entidade, ações operacionais por tela e CSV com LGPD/fórmula segura. Consulte `docs/diagnostico-enterprise-pos-rc-10.md`, `docs/security-lgpd-enterprise-pos-rc-10.md`, `docs/jornadas-enterprise-pos-rc-10.md`, `docs/matriz-crud-enterprise-pos-rc-10.md`, `docs/enterprise-manual-usuario-pos-rc-10.md` e `docs/enterprise-qa-checklist-pos-rc-10.md`.

## Pós-RC 14 — runtime, CI/CD e operação real

A consolidação Pós-RC 14 corrige o smoke com interpolação real, atualiza artefatos CI/CD para evidências Pós-RC 14, fortalece o Kanban autenticado/autorizado com dados Enterprise quando o schema existe, mantém fallback honesto para indisponibilidade de schema/provedor GED, e documenta pendências runtime que dependem de ambiente Docker/PostgreSQL/GED configurado.

Documentos principais:

- `docs/diagnostico-tecnico-pos-rc-14.md`
- `docs/evidencias-consolidacao-pos-rc-14.md`
- `docs/manual-usuario-sigov-pos-rc-14.md`
- `docs/manual-admin-sigov-pos-rc-14.md`
- `docs/checklist-homologacao-pos-rc-14.md`
