# Última execução

Data: 2026-09-10. RC51.02. Estado: PARCIAL / BLOCKED; runtime estático de migrations estabilizado, artefatos rastreados saneados, P0 PostgreSQL 16 bloqueado e SaaS Admin não iniciado.

- Branch: `codex/rc51-02-migration-runtime-saas-admin`, baseada em `origin/main` `ec799b75a6a1b3156566afc8a21216748f82df24`.
- Estado preexistente antes da sprint: `.vs`, `bin` e `obj` rastreados e modificados; `src/Sigov.Worker/appsettings.json` modificado fora do escopo.
- Causa raiz: `Sigov:Database:MigrationsPath = "database/postgres/migrations"` era resolvido contra `Directory.GetCurrentDirectory()`/ContentRoot em `src/Sigov.Api`, fazendo a API procurar `src/Sigov.Api/database/postgres/migrations`.
- Correção: `MigrationsPath` relativo foi removido do `appsettings.json` local da API; Docker mantém `/app/database/postgres/migrations`; o resolvedor agora considera ContentRoot, CurrentDirectory e BaseDirectory, sobe ancestrais, valida `manifest.json`, recusa `.vs/bin/obj/artifacts/TestResults`, registra origem e falha em ambiguidade.
- Caminho canônico local esperado para Visual Studio: `C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations`.
- Manifesto canônico atual: `C:\MNSOFT\SIGOV-PLUS\database\postgres\migrations\manifest.json`.
- Artefatos: 1.084 entradas removidas somente do índice Git; arquivos físicos de trabalho preservados. Diretórios afetados: `.vs`, `src/*/bin`, `src/*/obj`, `tests/*/bin` e `tests/*/obj`.
- Commit de retorno dos artefatos no main atual: `ec799b75` (`dsdsds`), com origem histórica detectada em `f6bb7f24` e `4a466e38` para amostras de artefatos.
- Prevenção: `.gitignore` passou a ignorar `*.dll`; `build-test` no CI agora depende de `tracked-artifacts`, garantindo o gate antes de restore/build/test.
- Testes: `MigrationSqlPolicyTests` ampliado na classe existente para cobrir raiz do repo, Visual Studio em `src/Sigov.Api`, ContentRoot, BaseDirectory em `bin/Debug/net10.0`, absoluto válido/inválido, relativo em ancestral, relativo ausente, múltiplos checkouts, manifesto em `bin`, Docker absoluto, pacote publicado e contrato `MANIFEST_OUTDATED`.
- Evidência local: `bash scripts/check-tracked-artifacts.sh` PASS; `python -m json.tool database/postgres/migrations/manifest.json` PASS; `dotnet clean sigov.sln` PASS; `dotnet restore sigov.sln --locked-mode` PASS; `dotnet build sigov.sln -c Release --no-restore --nologo -warnaserror` PASS; `dotnet test sigov.sln -c Release --no-build` PASS com UnitTests 385, IntegrationTests 123 e ApiTests 100; `bash scripts/check-api-route-conflicts.sh` PASS em 630 rotas; `git diff --check` PASS; `rg -n '^(<<<<<<<|=======|>>>>>>>)' src database tests docs .github` PASS.
- `BLOCKED: PostgreSQL 16 vazio, segunda passagem idempotente, upgrade legado, pós-condições, equivalência semântica, API ApplyPending/ValidateOnly, Swagger HTTP 200, login, MinhaCentral, logout, revogação, tenant suspenso, dois tenants e acesso cruzado não foram executados porque psql não está instalado/no PATH, não há ConnectionStrings__DefaultConnection/PG* no ambiente e o Docker daemon desktop-linux está indisponível.`
- `BLOCKED: SaaS Admin/Entitlements funcional não foi iniciado porque P0 PostgreSQL 16 não foi aprovado.`
- Próximo item exato: disponibilizar PostgreSQL 16 descartável e concluir P0.1-B. Após P0 e entitlement canônico aprovados, seguir para **RC51.03 — Indústria: Ordem de Produção integrada demanda/venda → BOM/ficha técnica → roteiro → reserva de materiais → OP → apontamento → consumo → qualidade → produto acabado → estoque → custos → rastreabilidade**.

---

Data: 2026-09-09. RC51.01. Estado: PARCIAL / BLOCKED; catálogo runtime corrigido, validação PostgreSQL 16 e fase SaaS Admin não liberada.

- Branch: `codex/rc51-01-history-manifest-saas-entitlements`; HEAD inicial `3efdc97520d0db9019182705a645ff2c975ff84b`, sem upstream disponível no ambiente.
- Remoto configurado: `https://github.com/devmnsoft/SIGOV-PLUS.git`; atualização de `origin/main` BLOCKED por proxy HTTP 403. O HEAD inicial é o próprio merge commit obrigatório.
- Causa raiz confirmada: o parser adicionava ao modelo runtime somente entradas `applyAutomatically=true`, e a validação do ledger confundia migrations históricas declaradas com versões desconhecidas.
- Correção: coleções explícitas declaradas, automáticas, excluídas e baseline; histórico validado contra todas as declaradas; DDL restrito às automáticas ausentes; excluídas ausentes reportadas como `Excluded`.
- Diagnóstico do runner agora registra caminho absoluto, SHA-256 e contagens do manifesto, maior versão, diretório corrente e base da aplicação; candidatos ambíguos são recusados.
- Manifesto canônico estático: `/workspace/SIGOV-PLUS/database/postgres/migrations/manifest.json`; SHA-256 `5bcc4eb4f0935ec77d77ca799f4fd12006dd901fa687447957d39e813f5675cd`. O runner registrará ambos na execução; a evidência runtime não pôde ser produzida sem .NET/PostgreSQL.
- `BLOCKED: dotnet restore/build/test, Swagger, login, MinhaCentral, logout e revogação porque o executável dotnet não está instalado.`
- `BLOCKED: PostgreSQL 16 vazio, reaplicação e upgrade com ledger histórico porque psql, Docker e instância descartável não estão disponíveis.`
- Fase SaaS Admin não iniciada, conforme a proibição de iniciar P1 antes de todas as validações P0 passarem.

Próximo item exato: “RC51.02 — Ordem de Produção industrial integrada: demanda/venda → reserva de materiais → OP → apontamento → consumo → qualidade → produto acabado → estoque → custos e rastreabilidade.” GED continua obrigatoriamente por último.

---

Data: 2026-09-09. RC51.00. Estado: PARCIAL / BLOCKED; P0 estático estabilizado, P0 runtime PostgreSQL 16 ainda pendente.

## Execução RC51.00

- Branch: `codex/rc51-00-p0-saas-industria-integrada`, baseada em `origin/main` `6159822b17e31950e4664eed898b2ddc62bde5d2`.
- Saneamento Git: artefatos `.vs`, `bin`, `obj`, `artifacts`, TRX, PDB, caches e backups locais removidos somente do índice; arquivos físicos preservados.
- CI: gate `tracked-artifacts` adicionado para impedir retorno de artefatos gerados rastreados.
- README consolidado para .NET 10/C# 14, PostgreSQL 16+, Dapper, execução local primária sem Docker, Docker opcional e status conservador dos módulos.
- Autenticação API: `SigovApiAuthenticationHandler` valida API key de `/api/v1` por hash em `sigov.api_key`, escopos em `sigov.api_key_escopo` e bearer de sessão persistente; middleware `ApiKeyV1Middleware` ficou responsável por tenant, escopo e auditoria.
- Sessão Web: login cria sessão persistente em `sigov.identidade_sessao`, cookie carrega referência mínima, validação ocorre por request, logout e troca de senha revogam sessões.
- Migration nova: `20260909120000_rc51_identidade_sessao_persistente.sql`.
- Manifest/scripts: `manifest.json`, `database/postgres/script_completo.sql`, `database/postgres/script_completo_dev.sql`, `database/script_completo.sql`, `script_completo.sql` e `script_completop.sql` sincronizados; baseline com 170 migrations incluídas e 5 excluídas.
- GED: `20260902000000_rc50_98_ged_workflow_branding_logo.sql` preservada como histórica, removida de aplicação automática/baseline porque cria schema físico `ged.*` e GED está fora da RC51.00.
- Testes reclassificados nas classes existentes: contratos textuais históricos de admin seed, Docker obrigatório, GED pronto, rotas antigas de menu e outbox legado passaram a validar o contrato atual.
- `Sigov.IntegrationTests` foi adicionado a `sigov.sln`; a suíte agora participa do restore/build/test padrão da solução.
- Dependências: `Testcontainers.PostgreSql` atualizado de 3.10.0 para 4.14.0 para remover bloqueio de `SSH.NET 2023.0.0` vulnerável.

## Evidência local RC51.00

- `dotnet restore sigov.runtime.slnf --locked-mode`: PASS.
- `dotnet build sigov.runtime.slnf -c Release --no-restore --nologo -warnaserror`: PASS, 0 erros, 0 avisos.
- `dotnet restore sigov.sln --locked-mode`: PASS.
- `dotnet build sigov.sln -c Release --no-restore --nologo -warnaserror`: PASS, 0 erros, 0 avisos.
- `dotnet test tests/Sigov.UnitTests/Sigov.UnitTests.csproj -c Release --no-build`: PASS, 371/371.
- `dotnet test tests/Sigov.ApiTests/Sigov.ApiTests.csproj -c Release --no-build`: PASS, 100/100.
- `dotnet test tests/Sigov.IntegrationTests/Sigov.IntegrationTests.csproj -c Release --no-build`: PASS, 123/123.
- `pwsh -NoProfile -File scripts/check-migration-governance.ps1 -StaticOnly`: PASS estático; P0 runtime permanece BLOCKED por PostgreSQL 16.
- `bash scripts/check-tracked-artifacts.sh`: PASS.
- `bash scripts/check-api-route-conflicts.sh`: PASS, nenhum conflito direto em 630 rotas API.
- `python3 scripts/validate-rc50-80.py`: PASS, 175 migrations, 4 baselines e 1006 views verificados.
- `git diff --check`: PASS.
- `python -m json.tool database/postgres/migrations/manifest.json`: PASS.
- `npx --yes yaml-lint .github/workflows/*.yml`: PASS.
- `git ls-files | rg '(^|/)(bin|obj|\.vs)(/|$)|\.(trx|pdb|suo|user)$'`: PASS, sem artefatos rastreados.
- `rg -n '^(<<<<<<<|=======|>>>>>>>)' src database tests docs .github`: PASS, sem marcadores de conflito.
- `docker version`: BLOCKED, cliente 29.4.3 instalado, daemon `dockerDesktopLinuxEngine` indisponível.

## Bloqueios RC51.00

- `BLOCKED: PostgreSQL 16 vazio, reaplicação, upgrade legado e equivalência semântica completa não foram executados localmente porque o Docker Engine não está acessível neste host e não há instância PostgreSQL 16 descartável configurada.`
- `BLOCKED: Swagger runtime HTTP 200, login CPF/CNPJ/e-mail, MinhaCentral, logout, revogação, dois tenants, tenant suspenso e fluxo industrial runtime não foram executados porque dependem do P0 PostgreSQL 16 aprovado.`
- P1 SaaS Admin funcional e P2 Ordem de Produção industrial integrada não foram iniciados além das correções de autenticação/catálogo necessárias ao P0.

Data: 2026-09-09. RC50.99. Estado: PARCIAL / BLOCKED; P0 não aprovado e fases posteriores não iniciadas.

## Fechamento RC50.99

- Branch final: `codex/rc50-99-foundation-saas-industry`, baseada em `f6bb7f24df58620d3c898d3f28f8b460d8c366b1`.
- Governança: 184 SQLs, 174 entradas e dez órfãos classificados; validação estática PASS.
- Baseline: 170 migrations incluídas e quatro excluídas por contrato explícito. Aplicação vazia e reaplicação idempotente PASS no PostgreSQL 18 diagnóstico; ledger com 170 versões.
- Migrations publicadas `20260902010000` (validador UUID de Compras) e `20260903130000` (LicitaPro) preservadas e retiradas da execução automática/baseline, com migrations forward-only posteriores ativas.
- Restore locked e build Release `-warnaserror`: PASS. UnitTests: 371/371. Swagger runtime: 11/11 e HTTP 200. ApiTests: 88/100, com 12 contratos estáticos históricos falhando.
- `BLOCKED: aplicação/reaplicação, upgrade legado e equivalência no PostgreSQL 16 não executados porque o Docker Desktop local não iniciou e não há outra instância PostgreSQL 16 descartável disponível.`
- `BLOCKED: login CPF/CNPJ/e-mail, Minha Central, logout, revogação, tenant suspenso, dois tenants e fluxo industrial não executados porque dependem do P0 de banco oficial aprovado.`
- Nenhum commit e nenhum PR foram criados. Artefatos `.vs`, `bin` e `obj` já rastreados no commit base foram preservados e não devem ser staged.

Próximo comando exato: em PostgreSQL 16 descartável, executar duas vezes `psql -X -v ON_ERROR_STOP=1 -f database/postgres/script_completo.sql`; depois executar upgrade legado e comparação de schema.

## Registro anterior da fase P0

## Git e preservação

- Repositório: C:/MNSOFT/SIGOV-PLUS. Seis projetos src, database/postgres/migrations e tests confirmados.
- Inicial: codex/evolucao-saas-industria-360, HEAD e5c7e6c5ef782b38c17a6c3822c779e67ec9b3a9, sem upstream.
- Remoto: https://github.com/devmnsoft/SIGOV-PLUS.git.
- Commit local e5c7e6c sem mesmo SHA remoto; árvore idêntica à publicação 9f6618d, incorporada na main pelo PR #376.
- Fetch seguro: origin/main avançou para 5ef7516c; nenhuma diferença de árvore em relação ao checkout inicial.
- Branch desta fase: codex/p0-governanca-migrations, criada de origin/main (tracking inicial origin/main).
- Estado inicial: sete appsettings modificados; backups SQL, .vs, bin/obj, .github/copilot-instructions.md, migration mobile e lock de integração não rastreados. São preexistentes e não pertencem a esta fase.
- Branches disponíveis inspecionadas com git branch -avv; main e branch anterior locais, além das branches remotas históricas. Inventário exato será registrado junto às evidências.

## Achados iniciais

184 SQLs e 174 entradas no manifesto; dez órfãs; zero versões duplicadas no manifesto; um prefixo incompatível. Histórico usa knownChecksums com pós-condições.
sigov.sln contém somente runtime: dotnet test nessa solução não é evidência de testes executados. Swagger já possui teste runtime, que será reaproveitado.

## Ambiente

SDK efetivo 10.0.400 por rollForward latestFeature; 10.0.100 não instalado. PostgreSQL local 18, sem PostgreSQL 16; Docker CLI existe, daemon ausente. ConnectionStrings__DefaultConnection e senhas PostgreSQL não fornecidas ao processo.

BLOCKED: banco vazio/legado PostgreSQL 16 não executado porque não há instância descartável 16 disponível.
BLOCKED: login/isolamento runtime não executados porque dependem de banco validado e contas de teste.

## Escopo ativo

Governança de migrations, build e testes existentes. P1/P2/P3/P4 e GED não iniciados. Nenhuma migration histórica será alterada.
