# RC50.53-PROD — plano de validação integrada

Data da execução: 2026-08-19 (UTC). Branch: `work`. Baseline: `20be32479a5732b04b9b4771a6c0c0ffc0ee127a`.

## Pendências herdadas da RC50.52

A RC50.52 deixou sem prova real a aplicação limpa e parcial do banco, build Release, inicialização de API/Web/Worker, Swagger, health, logins, páginas, permissões, LGPD, auditoria e ensaio de backup/restore. Todos esses itens continuam bloqueados; uma inspeção estática não substitui a execução.

## Ambiente encontrado

| Ferramenta | Resultado |
|---|---|
| .NET SDK | **ausente** (`dotnet: command not found`); versão não apurável |
| `psql` / PostgreSQL | **ausente** (`psql: command not found`); versão e estado do servidor não apuráveis |
| `pg_dump` | **ausente**; backup real bloqueado |
| `pg_restore` | **ausente**; restore real bloqueado |
| Git | `2.43.0` |
| Python | disponível; manifest validado |

Este container Linux não é o host Windows preferencial e não contém as duas dependências obrigatórias. Instalar o SDK compatível com os projetos e os clientes PostgreSQL (`psql`, `pg_dump`, `pg_restore`), disponibilizá-los no `PATH` e garantir PostgreSQL em `localhost:5432` antes da repetição.

## Comandos da próxima execução real

1. Exportar `PGPASSWORD` somente na sessão e executar `./scripts/db/backup-sigov.sh`.
2. Aplicar `database/postgres/script_completo_dev.sql` com `psql -v ON_ERROR_STOP=1` em schema limpo autorizado e depois em cenário parcial isolado.
3. Executar os três validadores de índices, validador de rotas, `dotnet clean`, `dotnet restore --locked-mode` e `dotnet build ... -warnaserror`.
4. Subir API, Web e Worker; sondar health/liveness/readiness/Swagger e realizar os dois fluxos de login.
5. Validar menus, dashboards e operações persistentes de permissões/LGPD/auditoria com usuários autenticados.
6. Restaurar o dump em banco separado, verificá-lo e executar `SIGOV_SMOKE_APPLY_DATABASE=true ./scripts/smoke-production-like.sh` com URLs da API/Web configuradas.

## Riscos e critérios de bloqueio

São P0 ambientais a ausência de .NET e PostgreSQL. São P0 de produto até prova contrária: banco limpo/parcial, build, API, Web, login, Swagger em Development, health, rotas críticas e backup/restore. Persistência de permissões/LGPD/auditoria e Worker permanecem P1 sem evidência. Qualquer migration, build ou endpoint crítico que falhe encerra o gate; nenhum resultado `SKIP` pode ser promovido a sucesso.
