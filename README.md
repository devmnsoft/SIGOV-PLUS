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

