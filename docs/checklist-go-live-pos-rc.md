# Checklist go-live Pós-RC 04

## Técnico

- [ ] `dotnet build sigov.sln` sem erro.
- [ ] `dotnet test sigov.sln` sem erro.
- [ ] `docker compose up -d` saudável.
- [ ] Health checks `/api/health/live`, `/ready`, `/db` OK.
- [ ] Smoke test gera Markdown e JSON.

## Dados e LGPD

- [ ] Seed demo aplicado apenas em Development/Homologation.
- [ ] Nenhum CPF/CNPJ/e-mail/telefone completo em telas ou CSV.
- [ ] Tokens, API keys e secrets nunca exportados em claro.
- [ ] Storage path não aparece em relatórios públicos.

## Fluxos

- [ ] Protocolo Web ponta a ponta.
- [ ] GED com hash e validação pública.
- [ ] Dashboard e Minha Central consultam tabelas reais quando disponíveis.
- [ ] Busca global audita pesquisas sensíveis.
- [ ] Relatórios aplicam tenant/permissão/auditoria.
- [ ] Outbox processa conforme `Workers__Outbox__Enabled`.
- [ ] POC não aprova crítico sem evidência real.

## Pendências honestas

- [ ] Dependências externas documentadas.
- [ ] ICP-Brasil/Gov.br/OCR não anunciados como funcionais sem provedor.
- [ ] PDF/DOCX somente se infraestrutura real estiver habilitada.

## Pós-RC 05 — hardening final

- [ ] Workflow CI `build-test` executado com restore, build Release e testes.
- [ ] Workflow CI `docker-build` executado para API, Web e Worker.
- [ ] Workflow CI `sql-validate` aplicou migrations e seed demo duas vezes.
- [ ] `docker compose up -d` validado em host com Docker.
- [ ] `scripts/smoke-test-sigov.ps1` gerou Markdown e JSON.
- [ ] Segurança/LGPD revisada conforme `docs/security-lgpd-hardening-pos-rc-05.md`.
- [ ] Performance básica revisada conforme `docs/performance-pos-rc-05.md`.
- [ ] Pacote `artifacts/release/sigov-plus-1.0.0-rc-final` gerado sem secrets.

Observação honesta: este container de agente não possui `dotnet`, `docker` ou `pwsh`; evidências finais devem vir do CI e do ambiente de homologação.

## Pós-RC 06 — validação real

- [ ] CI `sql-validate` usa `sigov.outbox_evento` e valida tabelas críticas.
- [ ] Seed demo aplicado duas vezes sem duplicar protocolos demo.
- [ ] API key demo valida hash e escopos pluralizados.
- [ ] Smoke autenticado gera Markdown/JSON e mascara chave.
- [ ] `docker-compose-e2e` anexou logs, smoke e schema report.
- [ ] Package release gerado com `.env.example` sanitizado e sem `.env` real/storage/certificados.
- [ ] Go-live check gerou `docs/go-live-check-result.md` e `.json`.

## Pós-RC 07 — Enterprise CRUD funcional

- Incluídas tabelas `sigov.enterprise_*` idempotentes para Comercial, OS, Estoque/Compras, Industrial/Manutenção, Indústria Produção, eventos e auditoria.
- Telas Enterprise existentes passam a usar template operacional com listagem real, formulário, detalhes, exportação CSV e avisos LGPD/fallback.
- Jornadas mínimas funcionais: proposta aprovada gera pedido; pedido gera OS; OS consome estoque; saldo negativo é bloqueado; plano preventivo gera OS.

## Pós-RC 08 — Checklist Enterprise
- [ ] Reexecutar `dotnet build sigov.sln --configuration Release` em ambiente com SDK .NET.
- [ ] Reexecutar `dotnet test sigov.sln --configuration Release` em ambiente com SDK .NET.
- [ ] Reexecutar `docker compose build --no-cache && docker compose up -d` em homologação.
- [ ] Aplicar seed Enterprise duas vezes e confirmar idempotência.
- [ ] Executar roteiro manual Enterprise Pós-RC 08 e anexar evidências.


## Pós-RC 09 — QA funcional Enterprise

- Diagnóstico criado em `docs/diagnostico-enterprise-pos-rc-09.md`.
- Evidências de homologação registradas em `docs/evidencias-enterprise-pos-rc-09.md` e `docs/evidencias-enterprise-pos-rc-09.json`.
- Manual de usuário e checklist QA criados para a jornada Enterprise navegável.
- UX Enterprise refinada com filtros, paginação, loading, detalhes, edição, inativação, restauração, CSV com tenant, toasts e fallback honesto.
