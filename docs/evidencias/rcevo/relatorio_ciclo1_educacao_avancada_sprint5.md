# Relatório Ciclo 1 · Sprint 5 — Educação Avançada (merenda escolar: cadastro base de cardápio com produtos + unicidade de período)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

Cadastro base de merenda vertical e multi-esfera (regra 24): o comando tipado de **cardápio com produtos** que já existia apenas como contrato (`EducacaoMerendaCriarCardapioRequest` / `IEducacaoMerendaCardapioService`) ficou **implementado, persistido e exercido ao vivo**, completando a base de dados de merenda sobre as 12 tabelas já criadas em RC50.48 (`educacao_merenda_*`):

- **Comando tipado `POST /api/educacao/merenda/cardapios/completo`** com `{escolaId, inicio, fim, produtos}`:
  - **Escopo institucional** da escola antes de gravar (mesma entidade do contexto autenticado) — reaproveita a validação da Sprint 2 #6 (`ESCOLA_INEXISTENTE` 404 / `ESCOLA_FORA_DO_ESCOPO` 409).
  - **Produtos válidos do tenant**: cada id deve existir e estar ativo em `sigov.educacao_merenda_produto`; ausência/inativo → `CARDAPIO_PRODUTO_INEXISTENTE` (404).
  - **Sem duplicidade**: produto repetido no pedido → `CARDAPIO_PRODUTO_DUPLICADO` (409); período (início) já ocupado por cardápio vigente → `CARDAPIO_PERIODO_EXISTENTE` (409); `fim < início` → 400 nomeada.
  - **Persistência transacional**: cardápio (`RASCUNHO`) + itens `educacao_merenda_cardapio_item` referenciando o cardápio (`referencia_id`) e espelhando `nome`/`unidade` do produto — tudo numa única transação atômica.
- **Unicidade de período vigente por escola × data de início** (regra de unicidade análoga à matrícula da Sprint 3): índice único parcial
  `ux_merenda_cardapio_tenant_escola_inicio` sobre `(tenant_id, escola_id, data_inicio)`
  com `is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')`.
  Estados terminais liberam o período para novo cardápio; o comando checa antes de gravar (falha nomeada 409) e o índice é o **backstop transacional**.
- **Máquina de estados S2 reaproveitada** no cardápio: `RASCUNHO→ATIVA→ENCERRADA` (terminal não reabre, `TRANSICAO_INVALIDA`).
- **Autorização**: rota `[Authorize]` do controller base avançado; sem permissão granular nova nesta fatia (o cadastros de merenda seguem a política do domínio).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011140000_evo_c1s5_merenda_cardapio_unicidade_periodo.sql` | novo: índice único parcial `ux_merenda_cardapio_tenant_escola_inicio` + pós-condição explícita (falha nomeada `S5-POSTCOND` se faltar o índice) |
| `database/postgres/migrations/manifest.json` | entrada `20261011140000` registrada (sha256 `d4536f3f798ea6446726ce4da90d3719293a511157a6e5e3a366775bc8ad7d86`); 223 migrations |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1` (+`-IncludeDevelopmentSeed` para os dev) → S5 presente nos 6 consolidados (219 incluídas/4 excluídas) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | `IEducacaoMerendaService` agora declara `CriarCardapioComItensAsync` (contrato que já existia como record vira método implementado) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | novas falhas nomeadas: `CardapioProdutoInexistenteException` (404), `CardapioProdutoDuplicadoException` (409), `CardapioPeriodoExistenteException` (409) |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | `CriarCardapioComItensAsync` (transação: escopo institucional → validações → checagem de período → insert cardápio → itens espelhando produto); `using Npgsql` |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | mapeamento HTTP das 3 novas exceções (404/409/409) |
| `src/Sigov.Api/Controllers/EducacaoMerendaController.cs` | rota `POST /api/educacao/merenda/cardapios/completo` |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `MerendaBase_Sprint5_CardapioComProdutos_UnicidadePeriodo` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado 223→224 segmentos para registrar a migration (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E9 (10 checks: fixture, produtos, cardápio+itens, backstop de índice, duplicidades, inexistente, período inválido, escopo, máquina) + CX ampliado (cardápio+itens+produtos+escolas+entidade S5) |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e**.
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`POSTCOND|true`; índice criado no schema `sigov`); manifest + 6 consolidados sincronizados (regras 6–8).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **62/62 PASS** (ts=2026-10-11T14:31:31Z),
  fixtures fictícios `c1e26-%`/`C1S3-%`/`C1S5-%` + entidade S5 (tenant 910001) aplicados e removidos
  ao final (resíduo zero); os 52 checks das Sprints 1–4 (E0–E8) revalidados na mesma rodada.
- Suítes (execução limpa após build): Unit **521/521** · Integration **162/162** · Api **158/158**
  (0 ignorados; total 841; o delta de +1 na Integration é o fato estático novo da Sprint 5).

## Evidência Tier-2 ao vivo (bloco E9 — cadastro base de merenda)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E9.0 | Fixture S5 (entidade fictícia 99999999000301 + escola `c1e26-s5-esc` na entidade do contexto + escola `c1e26-s5-esc-out` fora do escopo) | exit=0 |
| E9.1 | Cadastro base de produtos via API (`POST /api/educacao/merenda/produtos` ×2) | 200/200; ids 1/2 no banco |
| E9.2 | **Cardápio com produtos** (`POST …/cardapios/completo`, 2 produtos distintos) | 200 data=1; banco `RASCUNHO\|2026-11-02\|2026-11-30\|2 itens\|2 produtos-distintos` |
| E9.3 | **Backstop de unicidade**: insert SQL bruto de 2º cardápio RASCUNHO mesmo escola+início | exit=3 — `duplicate key … ux_merenda_cardapio_tenant_escola_inicio` |
| E9.4 | Cardápio com mesmo início via API | **409 `CARDAPIO_PERIODO_EXISTENTE`** nomeada (não 500) |
| E9.5 | Produto repetido no pedido | **409 `CARDAPIO_PRODUTO_DUPLICADO`** |
| E9.6 | Produto id inexistente (9999999999) | **404 `CARDAPIO_PRODUTO_INEXISTENTE`** |
| E9.7 | `fim < inicio` | **400** validação nomeada do período |
| E9.8 | Escola de outra entidade | **409 `ESCOLA_FORA_DO_ESCOPO`** (escopo institucional antes do cadastro; regras 11/24) |
| E9.9 | Máquina de estados S2 aplicada ao cardápio | `RASCUNHO→ATIVA 200 →ENCERRADA 200 →ATIVA 409` (`TRANSICAO_INVALIDA`; terminal não reabre) |
| CX | Limpeza do fixture (ampliada com cardápio+itens+produtos+escolas+entidade S5) | `restantes=0` |

(Blocos E0–E8 das Sprints 1–4 revalidados na mesma rodada: dashboard conjunto completo, CSV integral,
diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de versão, escopo
institucional, unicidade por oferta, transferência com linhagem, correção auditada de frequência,
fechamento/reabertura de diário, publicação de histórico escolar versionado — todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

- Nenhum defeito novo de produto nesta sprint: o contrato `CriarCardapioComItensAsync` existia mas
  **não tinha implementação nem rota** — a própria ausência era o gap. O E9 o exerceu ao vivo pela
  primeira vez e todos os caminhos (sucesso, duplicidade, inexistente, período inválido, escopo,
  terminal) responderam com falhas nomeadas, sem 500 e sem sucesso vazio simulado (regra 13).

## Resíduos declarados (regra 13)

- `validate-script-completop` falha em seed pré-existente `admin@sigov.local` (2 ocorrências já em
  HEAD; não introduzidas nesta sprint) — declarado, não mascarado.
- `rc50_25` permanece arquivo órfã do manifest (intocada, regra 8).
- A operação de merenda (distribuição/consumo/requisição de compra ligando o cardápio ao estoque e às
  compras) fica para a **Sprint 6 (merenda operação)** — esta sprint fecha somente o **cadastro base**.
- Itens de cardápio espelham `nome`/`unidade` do produto; a quantidade por item ainda não é parte do
  comando base (fica para a operação de distribuição, que define unidades de consumo por aluno/turma).
