# RC50.52 — Plano de fechamento técnico

## Inventário e prioridade
- **P0 ambiental aberto:** PostgreSQL/`psql` e SDK `dotnet` não estão instalados neste contêiner; aplicação real, build, Swagger, login e páginas autenticadas não podem ser promovidos como aprovados.
- **P0 funcional encontrado e tratado:** quatro mutações de permissões retornavam HTTP 501; passaram a usar vínculos Dapper por tenant. Foi incluída também a remoção de permissão de usuário, ausente na API.
- **P1 encontrados e tratados:** LGPD só expunha respostas vazias e não tinha mutações; auditoria não consultava persistência nem registrava exportação. Os fluxos essenciais agora consultam/persistem estruturas RC50.51/52.
- **P1 operacional:** scripts de smoke Linux/Windows e verificação das sete páginas críticas foram adicionados.
- **P2 remanescente:** integrações externas explicitamente preparatórias (e-SUS/SISAB, PIX, OCR/ICP-Brasil), homologação de policies item a item e revisão dos avisos conservadores de migrations históricas.

## Rotas e páginas críticas
API: `/api/seguranca/*`, `/api/lgpd/*`, `/api/auditoria/*`, Swagger e health. Web: `/Auth/Login`, `/MinhaCentral`, `/SystemHealth/ProjectStatus`, `/Observabilidade/Dashboard`, `/Seguranca/Dashboard`, `/Auditoria/Dashboard` e `/Lgpd/Dashboard`. Sem runtime, risco 404/500 permanece até o smoke integrado.

## Placeholders e 501
Os 501 essenciais estavam em `SegurancaController`. Segurança, LGPD e Auditoria tinham coleções vazias fixas, agora substituídas por consultas paginadas e SQL parametrizado. Placeholders HTML de campos de formulário e integrações preparatórias não são P0/P1 desta sprint.

## Migrations
A RC50.52 adiciona tenant/protocolo/resposta à solicitação de titular, sequência de protocolo e eventos de incidente. Manifest, checksum e quatro scripts consolidados foram sincronizados. A aplicação em PostgreSQL real permanece pendente exclusivamente pela ausência do cliente/servidor local.

## Comandos executados
`git status`, inventário Git, leitura dos relatórios RC50.44–51, `python -m json.tool`, três validadores de migration, buscas `rg`, validador de rotas, tentativa de `dotnet --info`, tentativa de `pg_isready`, `git diff --check` e smoke local.
