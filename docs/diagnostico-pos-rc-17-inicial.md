# Diagnóstico inicial Pós-RC 17

- SHA inicial local: `e9e1486c3de02b484db702ee54bc939d7a6a70b8`.
- Ambiente local: `dotnet`, `docker`, `docker compose`, `psql` e `pwsh` indisponíveis no contêiner de execução; por isso os comandos reais foram tentados e registrados como limitação ambiental, não como sucesso.
- Remoto `origin` indisponível no checkout local; não foi possível consultar o último run ID do GitHub Actions a partir do repositório local.

## Falhas informadas e análise primária

| Job | Etapa | Primeiro erro técnico real | Causa raiz | Correção aplicada/proposta | Prioridade | Tipo |
| --- | --- | --- | --- | --- | --- | --- |
| build-test | build Release | Serviços transversais operacionais não tinham contratos/implementações persistidas consolidadas | DI incompleto para tarefas, agenda, notificações, Kanban e outbox operacional | Contratos Application, implementações Dapper e registros DI adicionados | Alta | Primário |
| sql-validate | migrations PostgreSQL | Modelo operacional transversal ausente/parcial | Tabelas requeridas não existiam de forma uniforme e segura para banco limpo/parcial | Migration idempotente não destrutiva Pós-RC 17 criada | Alta | Primário |
| docker-build | publish API/Web/Worker | Consequência de build e DI | Falhas de compilação/restore bloqueiam publish | Estabilização de contratos e registros | Alta | Consequência |
| docker-compose-e2e | startup/health | Consequência de build/migrations | Migrations e serviços de runtime bloqueiam startup saudável | Migration e DI adicionados | Alta | Consequência |
| smoke-static | validações estáticas | Núcleo operacional sem persistência real comprovável | Fluxos apenas auditáveis/parciais | Serviços e tabelas reais adicionados | Média | Primário |
| release-package-check | pacote | Consequência de build/test/smoke | Pipeline bloqueado por etapas anteriores | Correções estruturais adicionadas | Média | Consequência |

## Comandos tentados

```bash
git fetch origin main
dotnet --info
docker --version
docker compose version
psql --version
pwsh --version
dotnet clean sigov.sln
```
