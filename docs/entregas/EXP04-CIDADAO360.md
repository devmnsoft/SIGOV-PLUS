# Entrega EXP04 — Cidadão360

## Resultado

- Portal premium responsivo e acessível com catálogo persistido, busca, categorias, cards, detalhe e linguagem cidadã.
- Solicitação autenticada com validação server-side, LGPD, protocolo sequencial, verificador, auditoria e comprovante.
- Consulta pública segura e timeline limitada a eventos marcados como visíveis ao cidadão.
- Área própria, dashboard interno e rotas integradas para Ouvidoria, agenda, atendimento, satisfação e FAQ.
- Migration idempotente com 15 tabelas, sequence, checks, FKs, índices e permissões.
- Manifest e seis scripts completos/baselines sincronizados com a migration.

## Segurança e LGPD

O contexto é fail-closed. A abertura exige `pessoa_id`; dados pessoais não são expostos nas consultas, URLs ou logs. A consulta pública exige código verificador. A finalidade/base legal pertencem ao catálogo/configuração persistidos e o aceite é temporal. Acesso ao protocolo e criação são auditados, com correlation ID e sem conteúdo sensível.

## Comandos

- `dotnet build`
- `python -m json.tool database/postgres/migrations/manifest.json`
- buscas `rg` para IDs manuais, antiforgery, validação e termos proibidos
- validação/smoke de rotas por inspeção do endpoint MVC e build

## BLOCKED

`BLOCKED: comando git pull origin main não executado porque o checkout fornecido não possui remote origin nem branch main.`

`BLOCKED: comando dotnet build não executado porque o SDK dotnet não está instalado no ambiente.`

`BLOCKED: comando psql -v ON_ERROR_STOP=1 -f database/postgres/migrations/20260827100000_exp04_cidadao360_portal_servicos.sql não executado porque o cliente psql e uma conexão PostgreSQL 16 não estão disponíveis no ambiente.`

`BLOCKED: comando pwsh scripts/generate-script-completop.ps1 -Verify -IncludeDevelopmentSeed não executado porque o PowerShell não está instalado no ambiente; checksums, presença da migration e igualdade byte a byte dos artefatos foram validados com Python, sha256sum, rg e cmp.`

`BLOCKED: comando smoke HTTP das rotas /Cidadao não executado porque o SDK dotnet não está instalado para iniciar a aplicação; os templates e endpoints foram verificados estaticamente.`

Validação PostgreSQL conectada depende de `ConnectionStrings__DefaultConnection`/instância PostgreSQL 16 disponível no ambiente; nenhum sucesso de banco é simulado.
