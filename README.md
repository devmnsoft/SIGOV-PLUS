# sigov

Plataforma SaaS de gestão pública municipal para operação real de prefeituras, câmaras, autarquias, fundos, secretarias e unidades descentralizadas.

## Stack

ASP.NET Core, C# 10, Clean Architecture, DDD, Dapper, PostgreSQL, API REST, Bootstrap 5, JavaScript puro, Serilog/ILogger, Docker e testes automatizados.

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
