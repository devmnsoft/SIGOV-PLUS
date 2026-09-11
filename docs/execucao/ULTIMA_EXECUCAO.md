# Última execução

Data: 2026-09-11. RC51.02H (central e navegação contextual). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: raiz `/workspace/SIGOV-PLUS`; branch `work`; HEAD inicial `ac06642902bab7b52373cbe4473432a349cf8967`; árvore limpa; sem remoto/upstream.
- Corrigidos totais/vencimentos reais, exercício selecionado, estados 403/503/vazio e removido o indicador GED sem fonte operacional.
- Favoritos e recentes foram isolados por identidade/contexto, com URL interna, descarte legado, revalidação de visibilidade e `textContent`.
- Paleta não simula disponibilidade com catálogo local; navegação ativa compara segmentos e aplica `aria-current`.
- `node --check` dos três scripts e `git diff --check`: PASS. Gates .NET/PostgreSQL, runtime e screenshots: BLOCKED por ausência das ferramentas.
- Evidência e matriz: `docs/entregas/RC51-02H-CENTRAL-NAVEGACAO.md`.

Data: 2026-09-11. RC51.02G (central de trabalho fail-closed). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`; HEAD inicial `d74a9d238156214f1f2301e562bd437565cc733a`; árvore limpa; checkout sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `pwsh`, `psql`, PostgreSQL 16 e navegador não estão disponíveis neste ambiente.
- Existente: `MinhaCentralService`, `pendencia_operacional`, shell autenticado e componentes visuais compartilhados. Lacuna confirmada: a central consultava todas as pendências abertas do tenant, sem limitar ao usuário responsável, e convertia contexto/schema/banco indisponível em painel vazio HTTP 200.
- Complementado: tenant e usuário agora são contexto obrigatório; o tenant precisa existir e estar ativo; a worklist usa query parametrizada por `tenant_id` e `responsavel_usuario_id`; ausência da estrutura ou falha de consulta é propagada e apresentada como indisponibilidade HTTP 503, com referência de correlação e sem pendências simuladas.
- Serviço canônico preservado: `MinhaCentralService` continua apenas agregando `pendencia_operacional` e encaminhando ao documento original; nenhum segundo workflow foi criado. Dependências: autenticação persistida, tenant ativo e migration `20260819150000`. Aceite desta fatia: usuário não recebe tarefa atribuída a outro usuário/tenant e indisponibilidade não aparenta sucesso.
- Validação estática: `git diff --check`, conflito de rotas, JSON do manifesto, artefatos rastreados e marcadores de conflito passaram. **BLOCKED separadamente:** restore/build/test, PostgreSQL 16 e screenshot real, por ausência das ferramentas/runtime.
- Próximo item exato: executar restore/build/test e validar no PostgreSQL 16 duas sessões do mesmo tenant com pendências atribuídas distintas; em seguida implementar a fila de aprovações disponíveis por autorização persistida, sem tornar pendência sem responsável visível por padrão.

---

Data: 2026-09-10. RC51.02F (diagnóstico independente da correção de fundação). Estado: PARCIAL / BLOCKED.

- Preflight: branch `work`; HEAD inicial `01d7f64f893a86b3c3cacf50f907aabe09bb487d`; árvore limpa; sem remoto e sem upstream configurados. SDK normativo `10.0.100`; projetos Domain, Application, Infrastructure, Api, Web, Worker e quatro projetos de teste confirmados.
- Matriz curta: Fundação — existente/PARCIAL: runner e aplicador com ledger/probes; lacuna: runtime PG16; serviço canônico: `MigrationRunner`; dependência: dotnet/pwsh/psql; aceite: vazio, upgrade e reaplicação. Visual/SaaS — existente/PARCIAL: componentes compartilhados, catálogo persistido e entitlements; lacuna: Gate A e runtime; serviços canônicos: avaliador/catálogo SaaS; dependência: Fundação; aceite: administração isolada por tenant. Indústria, Almoxarifado, Contratos, Frotas/Patrimônio, Educação/Saúde/Jurídico — existentes/PARCIAIS ou ESTRUTURA; lacuna: jornadas integrais e prova runtime; dependência: Gates A/B; aceite: invariantes específicos sem duplicar livros/autoridade. GED — PARCIAL e mantido por último.
- Lacuna corrigida: a migration corretiva `20260910120000` tinha uma pós-condição booleana agregada, que não identificava isoladamente permissão ausente/inativa, tipo/nulabilidade de `perfil_acesso.tenant_id`, tabela histórica ou função/trigger SaaS divergente. O manifesto agora contém cinco probes nomeados, com diagnóstico esperado/obtido, e mantém a pós-condição final forte.
- Preservado: migrations SQL publicadas e consolidados não foram alterados; não houve mudança de schema. O teste de regressão existente passou a exigir os cinco invariantes e a força da pós-condição final.
- Validação estática: JSON e `git diff --check` passaram. **BLOCKED separadamente:** restore/build/test (.NET ausente), parser PowerShell (`pwsh` ausente), PostgreSQL 16 limpo/upgrade/reexecução (`psql` ausente), Swagger/login/MinhaCentral/logout/isolamento e screenshots reais (runtime e navegador ausentes).
- Próximo item exato: executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; depois aplicar em PostgreSQL 16 vazio, reaplicar e executar upgrade legado autorizado, exigindo todos os cinco probes aprovados e `pendentes=0; checksum=0; falhas=0`. Somente então liberar Gate B SaaS.

---

Data: 2026-09-10. RC51.02E (ledger seguro no aplicador operacional). Estado: PARCIAL / BLOCKED.

- Branch observada: `work`; HEAD inicial `cd0f4c4e43ed0cdcbf4e57fd6ff6b3bc2039c471`; árvore inicialmente limpa; sem remoto e sem upstream configurados.
- Comportamento preservado: manifesto com 176 entradas/172 automáticas e probes determinísticos das três pós-condições; `MigrationRunner` .NET já recusava versão/checksum desconhecido.
- Lacuna confirmada: `apply-migrations-manifest.ps1` silenciava falha de leitura do ledger, não recusava versão desconhecida, recusava checksums históricos declarados e exigia `psql` até em `-ValidateOnly`. Também reaplicava compatibilidades finais quando nenhuma migration fora aplicada.
- Implementação: o aplicador agora distingue banco limpo de falha de consulta, valida toda versão do ledger contra o manifesto, aceita apenas checksum atual ou `knownChecksums` com pós-condição forte, revalida entradas históricas presentes, não executa `compatibilityAfterAll` numa segunda passagem sem pendências e permite validação estática sem cliente PostgreSQL.
- Critério/evidência: JSON, shell e `git diff --check` passaram; contratos de regressão foram ampliados na classe existente. **BLOCKED:** parser PowerShell, build/testes .NET e runtime PostgreSQL 16 não executados porque `pwsh`, `dotnet` e `psql` não existem no ambiente.
- Dependências e próximo item: executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; depois testar banco vazio, segunda passagem, ledger com versão desconhecida, checksum histórico conhecido/desconhecido e upgrade legado autorizado em PostgreSQL 16. Gate B continua bloqueado.

---

Data: 2026-09-10. RC51.02D (validação determinística das pós-condições). Estado: PARCIAL / BLOCKED.

- Branch observada: `work`; HEAD inicial `86a8cfcb3803221735426bf937924606c1c201ba`; working tree inicialmente limpa; branch sem upstream e checkout sem remoto configurado, portanto a comparação atualizada com `origin/main` ficou BLOCKED sem alterar o trabalho local.
- As correções forward-only e os probes separados das migrations `20260802210000`, `20260819120000` e `20260908120000` já existiam no HEAD inicial. A lacuna encontrada estava no aplicador PowerShell: ele executava apenas `postConditionSql`, ignorava `postConditionProbes` e, na reaplicação, pulava toda validação das versões presentes no ledger.
- Implementado: probes nomeados agora participam da mesma transação antes do registro no ledger. Ao final, o aplicador revalida a pós-condição booleana e cada probe de todas as migrations automáticas, inclusive quando o ledger já estava completo. Falhas preservam o nome da versão/invariante e não são convertidas em sucesso.
- Compras permanece com o rename conservador do bootstrap 070, contrato empresarial UUID e contrato governamental bigint separados; Educação/Saúde preserva template global anulável sem conceder perfil; SaaS preserva nome/disponibilidade comerciais administráveis e valida tabela, tipos, constraint, funções e triggers separadamente.
- **BLOCKED:** restore/build/test e parser PowerShell não executados porque `dotnet` e `pwsh` não existem no ambiente desta execução.
- **BLOCKED:** PostgreSQL 16 vazio, reaplicação, upgrade sanitizado, equivalência, Swagger, login/MinhaCentral/logout, isolamento e validação visual não executados porque `psql`, runtime e navegador não estão disponíveis/configurados.
- Próximo comando exato: `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; em seguida, com PostgreSQL 16 descartável configurado, executar aplicação vazia, reaplicação e upgrade sanitizado antes de liberar Gate B.

---

Data: 2026-09-10. RC51.02C (recalc + remoto). Estado: PARCIAL / BLOCKED. Gate A quase fechado em runtime Podman PG16; Gate B–D não iniciados.

## Preflight recalculado

- Branch atual: `codex/rc51-02c-foundation-saas-multimodulo` (criada de `c6e74776`).
- Branch anterior / PR #385: `codex/rc51-02c-foundation-saas-industria-evolucao` @ `c6e74776` (open, mergeable_state=unstable, CI status pending/vazio).
- `origin/main`: `dc7c1ac23f0c4ee95d7362e149157f2bec8284dc` (inalterado após fetch --prune).
- Remoto: `https://github.com/devmnsoft/SIGOV-PLUS.git`.
- Working tree: limpa após commits da RC; alterações locais de runtime (Podman/DB) não versionadas.
- Layout: `src/Sigov.{Api,Web,Application,Infrastructure,Domain,Worker}` + `tests/Sigov.{UnitTests,IntegrationTests,ApiTests,Testing}` + `database/postgres/migrations` confirmados.

## Inventário migrations (recalculado)

| Métrica | Valor |
|---|---|
| SQL em `database/postgres/migrations` | 185 |
| Entradas no manifesto | 175 |
| `applyAutomatically=true` | 171 |
| `includeInBaseline=true` | 171 |
| Excluídas de apply | 4 |
| Órfãs (SQL fora do manifesto) | 10 |
| Versões duplicadas no manifesto | 0 |
| Arquivos duplicados no manifesto | 0 |
| SHA-256 do manifesto | `10eda0576ee35a3db1d1c31d6fb2465a3ddb671c72fbda4f7bc8fc11f94a6f93` |
| Maior versão declarada | `20260909120000` |

Órfãs preservadas (não aplicadas automaticamente). Duas compartilham o prefixo de versão `20260813120000` **fora do manifesto**:
`20260813120000_rc50_24_educacao_rh_folha_produto_core.sql` e `20260813120000_rc50_27_operacoes_inteligentes.sql`.

## Gate A — evidência reconfirmada

- Runtime: Podman `postgres:16-alpine` `sigov-pg16-gatea` em `127.0.0.1:5433` (shm 1 GiB). Docker Desktop continua instável neste host.
- **PASS:** `bash scripts/check-tracked-artifacts.sh`
- **PASS:** senhas literais em appsettings versionados = 0 (`Password: ""` apenas)
- **PASS:** apply vazio = 171; reaplicação idempotente = 171\|171
- **PASS:** `script_completop.sql` one-shot em `sigov_gate_a_oneshot` = EXIT 0 (engine permaneceu Up)
- **PASS:** equivalência semântica colunas `information_schema` migração vs one-shot = **EQUIVALENT** (41 172 linhas cada)
- **PASS:** Swagger HTTP 200 (3 445 457 bytes) reconfirmado
- **PASS:** build Release `-warnaserror` (`sigov.runtime.slnf` e `sigov.sln`)
- **PASS:** testes 389 Unit + 123 Integration + 102 Api
- **PASS:** rotas API 630 sem conflito direto; `validate-rc50-80.py` PASS; governance static PASS
- **PASS auth:** login/login-email/CPF/CNPJ → MinhaCentral 200; logout POST → login; dois tenants (`admin`=SIGOV Local id5 vs `admin_t1`=Tenant de Desenvolvimento id1) com hero distinto (`ISOLATION_OK=True`)
- **BLOCKED:** upgrade legado formal (banco com ledger antigo → apply residual) não executado nesta recalculação
- **BLOCKED/parcial:** troca de senha, revogação de sessão, tenant suspenso e acesso cruzado API profundo não reexecutados ponta a ponta nesta passagem
- Claims `HasClaim("permission"|"permissao")` em src = 0; `RequestPermissionClaimsTransformation` permanece no-op
- Remanescente Gate B: dois `IModuleCatalogService` (Commercial sync hardcoded em Program + SaaS async `PersistentModuleCatalogService` no DI)

## Matriz de causas-raiz (estado atual)

| Problema | Causa raiz | Arquivo responsável | Teste reprodutor | Correção |
|---|---|---|---|---|
| Equivalência one-shot derrubava Docker | Engine Desktop/VHD instável + shm baixo | Docker Desktop / host | `psql -f script_completop.sql` | Podman WSL + `--shm-size=1g` → PASS |
| MinhaCentral 500 pós-login | Ícone `workflow` ausente + size 34 + Dapper record | `IconRegistry`, `SigovIconTagHelper`, `AtividadeRecenteViewModel` | Login → `/MinhaCentral` | Registrar ícones/sizes; VM com props → PASS |
| Claims pontuais | Controllers ainda usavam `HasClaim` | `MatrizAcessoController`, `CommercialControllers` | Grep `HasClaim("permission")` | Snapshot/serviço → 0 matches |
| Baseline incompleto | `enterprise_tenant_mapping` auto sem baseline | `manifest.json` | compare schema | `includeInBaseline=true` + scripts → 171 |
| Dois tenants sem evidência | Seed só no tenant 5 | seeds/runtime | Login `admin_t1` | Seed entidade/exercício/usuário tenant 1 → PASS hero |
| Dual catálogo | Interfaces homônimas em namespaces distintos | Commercial vs Saas.Modules | DI/Program | Pendente Gate B |
| Upgrade legado | Não executado | n/a | apply residual em ledger antigo | BLOCKED |

## Divergências vs auditoria `ec799b75` / docs anteriores

1. **Baseline 170 → 171:** docs antigos e auditoria falavam 170; após incluir `enterprise_tenant_mapping` no baseline, o valor recalculado é 171.
2. **Login HTTP:** estava BLOCKED (PG caiu); agora PASS (e-mail/login/CPF/CNPJ + MinhaCentral + logout).
3. **Equivalência:** estava BLOCKED por crash Docker; agora PASS em Podman (41 172 colunas equivalentes).
4. **Dois tenants:** estava BLOCKED; agora PASS no shell MinhaCentral (hero por tenant). Isolamento de dados/API profundo ainda parcial.
5. **Branch:** contrato pediu `codex/rc51-02c-foundation-saas-multimodulo`; PR #385 usava `...industria-evolucao`. Ambas apontam ao mesmo SHA `c6e74776` até o commit documental desta recalculação.
6. **BACKLOG item 1 (“184 arquivos”):** inventário atual = **185** SQLs.
7. **`validate-rc50-80` “4 baselines”:** refere-se ao conjunto de baselines/exclusões do validador, não a `includeInBaseline=171`.
8. **Gates 2–5 / B–D:** continuam **não iniciados** (Gate A ainda incompleto pelo upgrade legado formal).

## Não iniciado

Gate B SaaS canônico; Gate C OP industrial; Gate D Compras/Almoxarifado–Jurídico–Educação; backlog detalhado dos demais; GED por último.

## Próximo item exato

Executar **upgrade legado formal** em PostgreSQL 16 (ledger antigo → apply residual com `pendentes=0 checksum=0 falhas=0`) e fechar evidências de revogação/tenant suspenso/acesso cruzado API; só então liberar Gate B.

---

Data: 2026-09-10. RC51.02A (continuação). Estado: PARCIAL / BLOCKED. Gate A parcialmente evidenciado em runtime; Gate A não aprovado; Gate B e Gate C não iniciados.

- Branch: `codex/rc51-02a-foundation-saas-industria-core` sobre `origin/main` `2646b374` (merge do PR #383).
- Runtime PostgreSQL 16.15 descartável em `127.0.0.1:5433` (container `sigov-pg16-gatea`).
- **PASS:** apply vazio = 171 migrations automáticas registradas; segunda aplicação idempotente (skip de ledger) = PASS.
- **PASS:** Swagger HTTP 200 em API local (`/swagger/v1/swagger.json`, ~3,4 MiB) com `MigrationMode=Disabled` apontando ao banco Gate A.
- **PASS estático de senha:** `check-local-login.ps1` validou hash de `admin` e `superadmin`.
- Correções de suporte ao Gate A: `apply-migrations-manifest.ps1` (StrictMode, pós-condição via arquivo, skip de já aplicadas); manifesto alinhou pós-condições de `20260802210000` e `20260826130000` ao SQL publicado; `AuthenticationRepository` reordenou colunas para materialização Dapper de `AuthenticationUser`.
- **BLOCKED:** equivalência schema vs `script_completop.sql` — Docker Desktop derrubou o engine durante o apply/compare.
- **BLOCKED:** login HTTP ponta a ponta / MinhaCentral / logout / dois tenants — banco 16 caiu (connection refused) durante as tentativas; cookie autenticado não foi obtido.
- **BLOCKED:** upgrade legado formal e isolamento de dois tenants.
- Gate B e Gate C não avançaram (Gate A runtime incompleto).
- Próximo item: estabilizar PostgreSQL 16 descartável (sem colisão com PG18 na 5432), fechar equivalência/legado, login HTTP e-mail/CPF/CNPJ + isolamento; só então Gate B e Gate C.

---

Data: 2026-09-10. RC51.02A. Estado: PARCIAL / BLOCKED. Gate A estático reparado; Gate A runtime, Gate B e Gate C não aprovados.

- Branch: `codex/rc51-02a-foundation-saas-industria-core`, criada de `origin/main` `8b6e867b2e76638d5d809ca6a3462b1d7d60756c`.
- `origin/main` avançou após a auditoria `ec799b75`: merge do PR #381 e do PR #382 (RC51.02), depois `d45dc8e ajuste`, `377b178 sasa` e `8b6e867 aDASDAs`.
- Causa raiz da regressão: esses três commits recolocaram 14 arquivos de `obj` no índice Git e a senha literal no `src/Sigov.Worker/appsettings.json`.
- Correção Gate A (estática): `git rm --cached` dos 14 artefatos (arquivos físicos preservados); `.gitignore` e o gate passam a recusar `*.cache.json`; Worker versionado sem connection string.
- Preservado sem commit: alterações locais de `obj` geradas pelo build.
- Inventário: 185 SQLs, 175 entradas, 171 automáticas, 4 excluídas de apply, 170 baseline, 10 órfãs classificadas. `20260902010000`, `20260903130000` e `20260909120000` permanecem declaradas.
- Testes: UnitTests 389 (1 s), IntegrationTests 123 (5 s), ApiTests 102 (9 s). Restore/build Release `-warnaserror` PASS. `check-tracked-artifacts` PASS; `validate-rc50-80.py` PASS; rotas API 630 sem conflito direto; `git diff --check` PASS.
- `BLOCKED: Gate A runtime — PostgreSQL 16 vazio/reaplicação/legado/equivalência, Swagger HTTP 200 e login/isolamento não executados. psql ausente, Docker daemon desktop-linux indisponível, ConnectionStrings__DefaultConnection/PG* ausentes. PostgreSQL 18 local existe somente como diagnóstico e não substitui o gate 16.`
- `BLOCKED: Gate B não avançado — o contrato exige Gate A runtime verde. Catálogo Commercial hardcoded e telas SaaS Admin ainda incompletas em relação ao aceite B2 permanecem pendentes.`
- `BLOCKED: Gate C não iniciado — jornada de Ordem de Produção condicionada a A e B verdes. Telas industriais continuam genéricas (`ModulePage`) e o dashboard ainda usa timer de sucesso.`
- Próximo item exato: disponibilizar PostgreSQL 16 descartável, concluir vazio/reaplicação/legado/equivalência, Swagger 200 e login/dois tenants. Só então fechar Gate B (SaaS Admin completo) e Gate C (OP industrial). GED permanece por último.

---

Data: 2026-09-10. RC51.02. Estado: PARCIAL / BLOCKED; P0 estático e entitlement/SaaS Admin implementados no código; PostgreSQL 16 vazio/reaplicação/legado, Swagger HTTP 200 e login ponta a ponta permanecem BLOCKED neste host.

- Branch: `codex/rc51-02-p0-runtime-entitlement-canonico`, baseada em `origin/main` `ec799b75a6a1b3156566afc8a21216748f82df24` (HEAD inicial da sprint anterior `0675787277addc39df526515d431900f27e4248a` reaproveitada como fundação).
- Causa raiz 1: `check-tracked-artifacts.sh` imprimia PASS fora de worktree porque `git ls-files | rg` falhava dentro de `if`; agora falha se git/rg/worktree/ls-files falharem e só imprime PASS no final.
- Causa raiz 2: `MigrationRunner` resolvia o caminho no construtor e `DatabaseOptions.MigrationsPath` tinha default relativo enganoso. Construtor ficou lazy; `MigrationMode=Disabled` não toca o diretório.
- Causa raiz 3: `RequestPermissionClaimsTransformation` recolocava permissões/módulos no principal; sidebar e `UserPermissionService` decidiam por claims. Snapshot request-scoped (`IRequestAuthorizationSnapshot`) carrega uma vez por request; policies usam `PersistedPermissionHandler`.
- Causa raiz 4: dois `IModuleCatalogService` hardcoded. A interface canônica em Application.Saas.Modules passou a ser assíncrona; DI usa `PersistentModuleCatalogService` lendo `modulo_saas`. `IModuleEntitlementEvaluator` é a decisão única de contrato+dependência+permissão.
- SaaS Admin: SuperAdmin lista tenants (status, esfera, entidades, usuários ativos, módulos, última atividade), abre detalhe e contrata/suspende/reativa com justificativa, transação, concorrência e auditoria. Admin local não altera catálogo/preço e só vê o próprio tenant.
- Indústria: `RequireModule("industria_producao")`, menu e `IndustriaComercialService` usam o avaliador; SQL direto de contratação de `industria_producao` removido. Status permanece PARCIAL.
- Manifesto estático: 185 SQLs, 175 entradas, 10 órfãos classificados preservados; 171 automáticas, 170 baseline; `20260902010000`, `20260903130000` e `20260909120000` permanecem declaradas. SHA do manifesto deve ser relida no runtime.
- Testes: UnitTests 389, IntegrationTests 123, ApiTests 102. `bash scripts/check-tracked-artifacts.sh` PASS; `python -m json.tool database/postgres/migrations/manifest.json` PASS; `python scripts/validate-rc50-80.py` PASS; `bash scripts/check-api-route-conflicts.sh` PASS em 630 rotas; `git diff --check` PASS.
- `BLOCKED: PostgreSQL 16 vazio, segunda passagem idempotente, upgrade legado, equivalência, API ApplyPending/ValidateOnly, Swagger HTTP 200, login e-mail/CPF/CNPJ, MinhaCentral, logout, revogação, tenant suspenso, dois tenants e SaaS Admin ponta a ponta não foram executados porque psql não está no PATH, ConnectionStrings__DefaultConnection/PG* ausentes e o Docker daemon desktop-linux está indisponível.`
- ADR de catálogo/entitlements e de autenticação permanecem sem ACEITO até evidência runtime PostgreSQL 16.
- Próximo item após todos os gates verdes: **RC51.03 — Ordem de Produção industrial integrada: demanda/pedido → BOM versionada → roteiro versionado → disponibilidade e reserva atômica → liberação da OP → apontamento → consumo por lote → qualidade → entrada do acabado → custo real/variação → encerramento e rastreabilidade.**

---

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
