# Diagnóstico Pós-RC 06 — CI real, smoke autenticado e pacote Go-Live

Data: 2026-07-08.

## Sincronização e limitações do clone

- `git checkout main` foi tentado, mas o clone local possui apenas a branch `work`; foi criada a branch `codex/pos-rc-06-corrigir-ci-smoke-release` a partir do estado disponível.
- `git pull` não foi executável no fluxo inicial porque não havia branch `main` local/remota no clone entregue ao agente.

## Arquivos analisados

Foram inspecionados workflows de CI/release, scripts de smoke, schema report, seed demo, package, validate e go-live, migrations Pós-RC, middleware de API key, controllers API v1/Web e documentação Pós-RC 05.

## Bugs encontrados

1. CI validava `sigov.outbox` em vez de `sigov.outbox_evento`.
2. `schema-report.ps1` dependia de `docker exec sigov-postgres`, incompatível com PostgreSQL service do GitHub Actions.
3. Smoke usava `Regex.Escape` diretamente sobre variável possivelmente vazia/nula.
4. Resumo Markdown do smoke usava string sem interpolação.
5. Seed demo gravava hash fictício incompatível com SHA-256 hexadecimal do middleware.
6. Escopos demo estavam no singular e não batiam com `protocolos.*`, `documentos.*` e `tarefas.*`.
7. Package release tratava `.env.example` e docs com termos `api-key`/`webhook` de forma rígida demais e podia bloquear exemplo sanitizado.
8. Faltavam E2E Docker Compose autenticado e release-package-check no CI.

## Correções realizadas

- CI `sql-validate` aplica migrations, aplica seed duas vezes, valida idempotência, tabelas críticas, hash demo, escopos e ausência de API key clara.
- `schema-report.ps1` passou a aceitar `-Mode Docker|Psql|Auto` e saída local/CI.
- Seed demo grava hash SHA-256 hexadecimal, prefixo `sigov_demo`, status `ATIVA`, metadados de rotação e escopos pluralizados.
- Smoke usa chave demo somente quando `SIGOV_SMOKE_USE_DEMO_KEY=true`, mascara logs como `sigov_demo_****rotate`, gera Markdown/JSON com totais, tempos, status e falhas bloqueantes/não bloqueantes.
- Package release sanitiza `.env.example`, bloqueia `.env` real, certificados/chaves/dumps/storage/bak e gera manifest com SHA-256.
- Go-live check gera `docs/go-live-check-result.md/json` e valida artefatos/documentos/scripts essenciais.

## Validações executadas neste ambiente

- Validações estáticas por leitura de arquivos e geração de branch foram executadas.
- As validações com `dotnet`, `docker` e `pwsh` dependem das ferramentas do ambiente; quando indisponíveis, devem rodar no GitHub Actions pelos jobs adicionados.

## Limitações honestas

ICP-Brasil, Gov.br, OCR, SMTP, WhatsApp e integrações oficiais continuam dependentes de provedores/credenciais reais. Nenhuma dessas integrações foi simulada ou declarada como validada nesta sprint.

## Execução final local tentada

- `dotnet clean sigov.sln`, `dotnet restore sigov.sln`, `dotnet build sigov.sln --configuration Release` e `dotnet test sigov.sln --configuration Release` retornaram exit 127 porque `dotnet` não está instalado neste container.
- `docker compose down -v`, `docker compose build --no-cache`, `docker compose up -d` e `docker compose ps` retornaram exit 127 porque `docker` não está instalado neste container.
- `pwsh -NoProfile -File scripts/go-live-check.ps1 -AllowWarnings` e `pwsh -NoProfile -File scripts/package-release.ps1 -Version 1.0.0-rc-final` retornaram exit 127 porque `pwsh` não está instalado neste container.
- `git diff --check` não encontrou erro de whitespace; apenas avisos de normalização LF/CRLF em scripts PowerShell.
