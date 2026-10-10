# RELATORIO GATE B (P1) — RC-SAAS-AUT validado ao vivo em PostgreSQL 16

Data: 2026-10-10 (UTC)
Prova de runtime executada em HEAD `c1fbf922` (base da RC-SAAS-AUT já entregue, commit `74f763`).
Consolidação do dual-catalog executada em `f388b67d` (mesma árvore; mudança de compilação/comportamento-neutra, re-verificada por build `-warnAsError` e pelas três suítes).
App: `Sigov.Web` (porta **5099**) + `Sigov.Api` (porta **5100**), builds locais do repositório corrente contra o banco oficial, com `Sigov__Database__MigrationMode=Disabled` (schema já migrado; o gate não reaplica migrations).
Banco oficial: **PostgreSQL 16.14** no container `sigov-postgres` (imagem `postgres:16`), banco `p1_dev`.
Gate driver: `scripts/gate-b-saas-runtime.ps1` (autocontido e reproduzível; aplica o fixture idempotente, executa G0–G5 e emite `result.json` / `report.md` / `log` em `artifacts/gate_b_saas_aut/`).
Fixture: `database/postgres/seeds/homologacao/20261010_gateb_rcsaas_aut_fixtures.sql` (select-or-insert, sem senha/token/chave/dado pessoal, placeholders `{{TBL}}`/`{{COL}}`).

## O QUE FECHA ESTE RELATÓRIO

Gate B (P1) exige provar, com banco oficial validado, o núcleo de **autorização SaaS (RC-SAAS-AUT)** descrito em `docs/SAAS-AUTORIZACAO-RC-SAAS-AUT.md` e condicionado no ADR (`docs/adr/ADR-SaaS-Catalogo-Entitlements.md`: "ACEITO somente após runtime PostgreSQL 16"). Este relatório prova ao vivo, em PG16: (1) cadeia de autenticação Web→bearer (`GET /Auth/ApiToken` minta o token bruto; cookie ≠ bearer); (2) o **avaliador canônico fail-closed** diferenciando os cenários comerciais A/B/C/D; (3) o **403 padronizado com `motivo`** em **paridade Web+API**; (4) a **revogação por alteração de identidade em paridade Web+API (A5)**; e (5) a **autoridade do admin global MNSOFT vinda do banco (regra 11)**. O resultado é **26/26 PASS, 0 FAIL**, capturado em `gate-b-saas-aut-result.json`. Este relatório também documenta a **consolidação do dual-catalog** (duas interfaces homônimas `IModuleCatalogService`) que removeu a ambiguidade de autoridade mantendo o banco como fonte única de decisão.

## NOTA DE AMBIENTE (não mascarada, regra 13/18)

- O host não possui `psql`; o SQL do gate é gravado em arquivo temporário no host, copiado via `docker cp` (origem com barra invertida do Windows → destino POSIX `/tmp/` do container) e executado com `docker exec sigov-postgres psql ... -f`. As comparações exatas usam consultas de coluna única porque `Out-String` converte TABs do psql em espaços.
- A porta `5432` do host é sombreada por um Postgres nativo do Windows; o **PG16 oficial** foi garantido via forwarder `alpine/socat` na porta livre `5433` → `sigov-postgres:5432` (container `p1fwd`, rede `sigov-plus_default`). Conexão confirmou `16.14`.
- O container `sigov-api` do stack pré-existente estava em loop de reinício (estado ambiente, ledger antigo vs. manifest); **não** foi usado — Web e API foram executados localmente (5099/5100) contra `p1_dev`, que é o caminho relevante para a jornada.
- **Artefato de captura de codificação (não defeito):** os valores de `evidence` de alguns checks aparecem com `Ã§`/`Ã£` (ex.: `"AutenticaÃ§Ã£o obrigatÃ³ria."`) porque a captura via console re-interpretou UTF-8 como Latin-1. A API/Web retornam UTF-8 correto (visível no `message` integral em G2/B e G2/C); só o snapshot de console do gate distorce o acento. Não afeta o veredito dos checks.
- O check `auth.api-token-mint` registra `erro='Nenhum exercício ativo'` como **informativo**: o endpoint `GET /Auth/ApiToken` ainda emite o token bruto de 64 chars quando não há exercício vinculado ao perfil; a validade do token é confirmada de forma independente em `auth.bearer-api-200` (HTTP 200).

## FIXTURES (dados fictícios, idempotentes, sem senha em texto puro)

Os quatro cenários A/B/C/D existem desde a seed homolog `database/postgres/seeds/homologacao/20261005_rcsaas_aut_seed.sql` (tenants `910001`…`910004`, usuários `hom.a/b/c/d.admin`, senha dev PBKDF2 para `Homologacao!2026`, contratos em `tenant_modulo_contratado` família B/C + espelho em `saas_plano`).

O fixture idempotente deste gate (`20261010_gateb_rcsaas_aut_fixtures.sql`) complementa sem recriar:
- Perfis **`ADMIN_TENANT` por tenant** (ids 90–93, para 910001–910004) com permissões todas-ativas, cujo `tenant_id` é derivado pelo gatilho de propagacao (resolvendo o escopo de permissão por contexto de tenant — sem conceder tripla global fora de escopo).
- Usuário-probe **`gate.nao.admin`** (id `910310`, tenant `910001`) com perfil restrito **`GTB_OPERADOR`** (sem a permissão de gestão de usuários) — usado para provar a negação `SEM_PERMISSAO` na paridade Web.
- Reassociação limitada dos grupos de cenário aos perfis `ADMIN_TENANT` (faixa 910401–4) e fixação do grupo 910410 → `GTB_OPERADOR`.

## RESULTADO GLOBAL

Fonte: `docs/evidencias/rcevo/gate_b_saas_aut/gate-b-saas-aut-result.json` (26 checks). Veredito consolidado: **PASS 26 · FAIL 0**.

| # | Fase | Item RC-SAAS-AUT | Esperado | Obtido (evidência resumida) | Veredito |
|---|---|---|---|---|---|
| 1 | G0 | Ambiente PG16 alcançável | banco oficial 16+ conectado | `PostgreSQL 16.14 … 64-bit` | **PASS** |
| 2 | G0 | Web no ar | `GET /Auth/Login` → 200 | `-> 200` | **PASS** |
| 3 | G0 | API no ar (autenticada) | rota autenticada sem bearer → 401 | `GET /api/saas/tenant/modulos -> 401` | **PASS** |
| 4 | G0 | Fixture aplicado | exit 0, idempotente | `aplicado exit=0` | **PASS** |
| 5 | G0 | Cenários presentes | 4 tenants de cenário | `linhas cenario=4` | **PASS** |
| 6 | G0 | Cenário B SUSPENSA | status comercial SUSPENSA | `910002\|t\|SUSPENSA\|2027-08-31\|10\|SUSPENSO` | **PASS** |
| 7 | G0 | Cenário C expirada | `data_fim` no passado | `910003\|t\|ATIVA\|2026-09-30\|10\|HABILITADO` (fim 2026-09-30 < hoje) | **PASS** |
| 8 | G1 | Login Web | POST `/Auth/Login` → 302 | `hom.a.admin -> 302 /MinhaCentral` | **PASS** |
| 9 | G1 | MinhaCentral autenticada | `GET /MinhaCentral` → 200 | `-> 200` | **PASS** |
| 10 | G1 | Mint do bearer | `GET /Auth/ApiToken` → token bruto 64 | `len=64` (msg informativa `Nenhum exercício ativo`) | **PASS** |
| 11 | G1 | Bearer válido na API | rota com bearer → 200 + módulos contratados | `-> 200 {success:true,data:[financeiro(contratado,habilitado), educacao…]}` | **PASS** |
| 12 | G1 | Cookie ≠ bearer | cookie cru `SIGOV.AUTH` como bearer → 401 | `-> 401` (cookie DataProtection ≠ token bruto) | **PASS** |
| 13 | G1 | Anônimo → 401 | sem bearer → 401 `authentication_required` | `-> 401 {"error":"authentication_required",…}` | **PASS** |
| 14 | G2 | **A** ATIVA/válida | módulo acessível → 200 | `GET /api/educacao/escolas -> 200` | **PASS** |
| 15 | G2 | **B** SUSPENSA | 403 `BLOQUEADO_COMERCIAL` | `403 motivo=BLOQUEADO_COMERCIAL` (login.ok=True; mensagem "Assinatura comercial … SUSPENSA; acesso suspenso.") | **PASS** |
| 16 | G2 | **C** expirada | 403 `CONTRATO_EXPIRADO` | `403 motivo=CONTRATO_EXPIRADO` (mensagem "Vigência da assinatura comercial expirada.") | **PASS** |
| 17 | G2 | **D** ATIVA/limite=1 | limite não bloqueia módulo → 200 | `-> 200` (limite aplica à criação de usuário, não a módulo) | **PASS** |
| 18 | G3 | Envelope 403 API padronizado | `success:false,data:null,motivo` | `API 403 success=False motivo=BLOQUEADO_COMERCIAL (motivo presente e padronizado)` | **PASS** |
| 19 | G3 | Paridade Web `SEM_PERMISSAO` | usuário sem gestão → 403 `SEM_PERMISSAO` | `gate.nao.admin GET /Seguranca/Usuarios -> 403 motivo=SEM_PERMISSAO ok=False` | **PASS** |
| 20 | G3 | Paridade Web admin 200 | admin global → 200 | `hom.a.admin GET /Seguranca/Usuarios -> 200 len=96000` | **PASS** |
| 21 | G4 | Antes do bump válido | api=200 e web=200 | `antes do bump: api=200 web_MinhaCentral=200` | **PASS** |
| 22 | G4 | Revogação Web (A5) | após bump identity, mesmo cookie → 302 | `web /MinhaCentral com mesmo cookie -> 302 (redirect p/ login)` | **PASS** |
| 23 | G4 | Revogação API (A5) | token antigo → 401 | `api com token antigo -> 401` | **PASS** |
| 24 | G4 | Estado restaurado | `updated_at` restaurado | `restaurado para 'null' (era 'null')` | **PASS** |
| 25 | G5 | Tripla no banco (regra 11) | permissão MNSOFT presente/ativa/módulo saas | `permissao saas.plataforma.administrar id=1755 ativo=t modulo=saas` | **PASS** |
| 26 | G5 | Holders ativos no banco | usuários ativos com a tripla (autoridade no banco) | `usuários ativos com o triplo = 8` | **PASS** |

## DETALHES POR FASE

### G0 — Ambiente e fixture
O gate confirma que o PG16 oficial está acessível (16.14), que Web (5099) e API (5100) respondem, e aplica o fixture idempotente antes de qualquer asserção funcional. A validação prévia dos cenários (B=SUSPENSA, C expirada por `data_fim` no passado) garante que a diferenciação observada em G2 vem **do estado do banco**, não de configuração em memória.

### G1 — Cadeia de autenticação (Web sessão → bearer API)
A diferenciação que importa para a RC-SAAS-AUT é a origem do bearer: o cookie `SIGOV.AUTH` é embrulhado por DataProtection (~1072 chars) e **não** é um bearer utilizável. O fluxo correto é login no Web → `GET /Auth/ApiToken` (cookie-auth) → JSON `{token, exercicioId, erro}` cujo `token` (64 chars, base64url) é o bearer enviado em `Authorization: Bearer`. O handler da API faz SHA‑256 do bearer → `identidade_sessao.token_hash` e valida a `auth_version` (revogação A5). Prova negativa inclusiva: usar o cookie cru como bearer dá 401, e sem bearer dá 401 `authentication_required`.

### G2 — Avaliador canônico fail-closed (crown jewel)
Com alvo `GET /api/educacao/escolas` (`[RequireModule(...Modulo="educacao")]`; tenants de contrato contratam `educacao`+`financeiro`), o avaliador diferencia por estado do tenant resolvido a partir dos claims do bearer:
- **A** (ATIVA/vigência futura) → 200;
- **B** (SUSPENSA) → 403 `BLOQUEADO_COMERCIAL` (status da assinatura comercial bloqueia);
- **C** (vigência expirada, `data_fim < hoje`) → 403 `CONTRATO_EXPIRADO`;
- **D** (ATIVA, limite=1) → 200, confirmando que **limite numérico não bloqueia módulo** (limita criação de usuário), ou seja, o bloco comercial é por estado/vigência, não por cota.
Cada login de cenário teve `login.ok=True`, provando que a negação vem da decisão de entitlement (fail-closed), **não** de falha de login.

### G3 — 403 padronizado em paridade Web+API
A RC exige um envelope único. Na API, `RequireModuleAttribute`/`IModuleEntitlementEvaluator` emite `{"success":false,"data":null,"message":…,"motivo":…}`. No Web, o alvo é `GET /Seguranca/Usuarios` (`SegurancaController.CanUsuarios` = tripla global `saas.plataforma.administrar` **ou** delegável `cliente.usuarios.gerenciar`); a negação passa por `ForbiddenResponse.Registrar` → `403 {ok:false,motivo:<wire>,detalhe}`. Provado: `gate.nao.admin` (perfil restrito) → 403 `motivo=SEM_PERMISSAO` e ainda alcança `/MinhaCentral` 200 (é permissão, não autenticação); `hom.a.admin` (admin global) → 200. O formato de `motivo` é compartilhado entre as duas superfícies.

### G4 — Revogação por alteração de identidade (paridade A5, Web+API)
Baseline `api=200 / web_MinhaCentral=200`. Bump em `usuario.updated_at` (regra de derivação da `auth_version` sobre `greatest(updated_at/created_at)`) torna a versão persistida obsoleta. Com a mesma sessão: Web `GET /MinhaCentral` → **302** (redirect para login, `OnValidatePrincipal` → `RejectPrincipal`), e a API com o **token antigo** → **401**. Depois, `updated_at` é restaurado para o valor original (`'null'`), mantendo o banco limpo. Demonstração de que revogar identidade invalida **ambas** as superfícies, em paridade.

### G5 — Autoridade do admin global MNSOFT no banco (regra 11)
Em vez de hardcode, a tripla MNSOFT existe no banco: permissão `saas.plataforma.administrar` (id 1755, `ativo=t`, `modulo=saas`) e **8** usuários ativos a possuem via perfis/grupos persistidos. Isso ancora o `CanUsuarios` do Web e o SuperAdmin como decisões derivadas do banco (fonte de autoridade), conforme regras 11/12.

## CONSOLIDAÇÃO DUAL-CATALOG (commit `f388b67d`)

Existediam **duas interfaces homônimas** `IModuleCatalogService` em dois namespaces + um catálogo estático de fallback implementando a interface de decisão:
- `Sigov.Application.Commercial.IModuleCatalogService` (síncrono, estático) = **catálogo de navegação/comercial** (singleton, exibição).
- `Sigov.Application.Saas.Modules.IModuleCatalogService` (assíncrono) = **interface canônica de decisão**, registrada **apenas** como `PersistentModuleCatalogService` (DapperContext, `modulo_saas`/`tenant_modulo_pacote`, fail-closed) em `DependencyInjection.cs` (scoped). O estático `Saas.Modules.ModuleCatalogService` **não** é registrado em produção — serve apenas como double determinístico dos testes unitários (`new ModuleCatalogService()` em `ModuleAccessCheckerTests`).

Mudança (comportamento-neutra, só compilação/claridade):
- Renomeada a interface de navegação para **`ICommercialModuleCatalogService`** (implementação + registros em `Web/Program.cs` e `Api/Program.cs` + consumidores `ExecutiveDashboard`, `Api/ModuleCatalogController`, `Web/ModulosController`, `Web/ModuleAccessService`), desambiguando o par.
- A **decisão de módulo continua** ancorada em `Saas.Modules.IModuleCatalogService` → somente `Persistent*` (banco autoridade, fail-closed). Nenhuma rota de decisão passa a depender do catálogo estático.
- **Preservação de testes (regra 14):** nenhum teste referencia o nome das interfaces; os arquivos foram mantidos no mesmo caminho (testes leem por caminho literal); o estático continua construível sem argumentos. Nada de teste novo adicionado.

Re-validação determinística neste host (mesma árvore, pós-refatoração):
- Build `sigov.tests.sln` Release `-warnAsError`: **0 avisos / 0 erros**.
- Suítes preservadas: UnitTests **508/508**, IntegrationTests **158/158**, ApiTests **158/158** — **0 ignorados** (mesma baseline de P0.2).

## ESTADO FINAL DO AMBIENTE

- Banco `p1_dev` **preservado e utilizável** (não é descartável): o fixture idempotente re-aplica sem duplicar; `usuario.updated_at` do cenário D restaurado a `'null'`. Nenhum dado pessoal/senha em texto plano foi adicionado (seeds fictícias, regra 9/10).
- Forwarder `p1fwd` (socat 5433→5432) e apps locais Web (5099) / API (5100) continuam ativos para os gates seguintes; serviço Windows de Postgres intocado.
- Artefatos gerados pelo gate persistidos (gitignored) em `artifacts/gate_b_saas_aut/` e **bankados** (versionados) em `docs/evidencias/rcevo/gate_b_saas_aut/`.

## LACUNAS / RESÍDUOS (não mascarados, regra 13)

- **Acesso cruzado em nível de API (`TenantAccessGuard`)** ainda não evidenciado explicitamente neste gate: isolamento entre tenants foi provado no nível Web/contexto em P0.4; a prova de "usuário do tenant X não alcança recurso do tenant Y pela API" permanece como evidência pendente (registrada também no BACKLOG).
- Operações administrativas **comerciais** de mutação (criar/downgrade/suspirar/cancelar plano, limites, convites/ACEITE FUNCIONAL) e a **troca de contexto com auditoria** estão cobertas deterministicamente pelas suítes (508/158/158 verdes) mas **todas** não foram executadas ao vivo neste relatório; a vertical de **autorização** (condição de aceite do ADR) sim.
- Docker `sigov-api` em loop (estado ambiente) contornado executando a API localmente; sem efeito na jornada.

## ARTEFATOS

Versionados em `docs/evidencias/rcevo/gate_b_saas_aut/`:
- `gate-b-saas-aut-result.json` — saída estruturada: 26 checks com fase/nome/passado/evidência/timestamp (**fonte de verdade**).
- `gate-b-saas-aut-report.md` — visão markdown gerada pelo driver (espelha o JSON).

Local/reproduzível (não versionado; o repositório ignora `*.log` em `.gitignore:34`):
- `gate-b-saas-aut.log` — trilha linha-a-linha dos checks; o driver a reescreve em `artifacts/gate_b_saas_aut/` a cada execução.

Reprodução: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/gate-b-saas-runtime.ps1` (requer PG16 via 5433, Web 5099 e API 5100 no ar contra `p1_dev`; reescreve `artifacts/gate_b_saas_aut/`).
