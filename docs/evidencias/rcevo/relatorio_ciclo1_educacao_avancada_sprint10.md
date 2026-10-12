# Relatório Ciclo 1 · Sprint 10 — Educação Avançada (indicadores + Educacenso + projeção de matrícula)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **vertical de indicadores de planejamento + censo Educacenso** multi-esfera (regra 24) sobre as tabelas `sigov.educacao_indicador*`/`sigov.educacao_projecao_matricula`/`sigov.educacao_educacenso_*` criadas na RC50.48: cinco comandos tipados novos e um dashboard de censo dedicado:

- **Comando tipado `POST /api/educacao/indicadores/catalogo/lancar`** com `{codigo, nome, tipo?, descricao?}`:
  - `codigo` e `tipo` normalizados para maiúsculas (comparação case-insensitive declarada); `tipo` ausente → `'INDICADOR'`.
  - **Um indicador vigente por código × tenant**: repetição → **409 `INDICADOR_CODIGO_EXISTENTE`**, com o índice único parcial `ux_indicador_tenant_codigo` como backstop transacional.
  - Indicador criado direto `ATIVA` com auditoria `LANCAR_INDICADOR`.
- **Comando tipado `POST /api/educacao/indicadores/valores/lancar`** com `{indicadorId, ano, valor}`:
  - Indicador inexistente/inativo → **404 `INDICADOR_INEXISTENTE`**; indicador não vigente (ex.: `RASCUNHO`) → **409 `INDICADOR_NAO_VIGENTE`** (escopo institucional reutilizado).
  - `ano` em `[2000, 2100]` e `valor > 0` (senão **400 validação explícita**).
  - **Um valor vigente por indicador × ano × tenant**: repetição → **409 `VALOR_INDICADOR_EXISTENTE`**, com o índice único parcial `ux_indicador_valor_tenant_ref_ano` como backstop transacional. `referencia_id` guarda o indicador e `ano_letivo` guarda o ano (mapeamento declarado nas tabelas largas rc50.48); `tipo='INDICADOR_VALOR'`.
- **Comando tipado `POST /api/educacao/projecoes-matricula/lancar`** com `{escolaId, ano, quantidade}`:
  - **Escopo institucional reutilizado do S2**: escola inexistente/inativa → **404 `ESCOLA_INEXISTENTE`**; escola de outra entidade → **409 `ESCOLA_FORA_DO_ESCOPO`**.
  - `ano` em `[2000, 2100]` e `quantidade > 0` (senão **400 validação explícita**).
  - **Uma projeção vigente por escola × ano × tenant**: repetição → **409 `PROJECAO_MATRICULA_EXISTENTE`**, com o índice único parcial `ux_projecao_matricula_tenant_escola_ano` como backstop transacional. Planejamento de oferta válido para qualquer esfera (prefeituras, secretarias estaduais, ministérios — regra 24).
- **Comando tipado `POST /api/educacao/educacenso/lotes/lancar`** com `{ano}`:
  - `ano` em `[2000, 2100]` (senão **400 validação explícita**); lote criado direto `ATIVA` com `tipo='EDUCACENSO'` e auditoria `LANCAR_LOTE_EDUCACENSO`.
  - **Um lote vigente por ano × tenant**: repetição → **409 `LOTE_EDUCACENSO_ANO_EXISTENTE`**, com o índice único parcial `ux_educacenso_lote_tenant_ano` como backstop transacional.
- **Comando tipado `POST /api/educacao/educacenso/inconsistencias/lancar`** com `{loteId, gravidade, origem, recomendacao?}`:
  - Lote inexistente/inativo → **404 `LOTE_EDUCACENSO_INEXISTENTE`**; lote não vigente (ex.: `RASCUNHO`) → **409 `LOTE_EDUCACENSO_NAO_VIGENTE`** (escopo institucional reutilizado).
  - `gravidade` restrita ao conjunto `{BAIXA, MEDIA, ALTA}` case-insensitive (senão **400 validação explícita**); `origem` obrigatória.
  - Mapeamento declarado nas tabelas largas: `tipo` = gravidade, `nome` = origem (maiúsculas), `descricao` = recomendação, `referencia_id` = loteId; inconsistência criada `ABERTA` com auditoria `LANCAR_INCONSISTENCIA_EDUCACENSO`. Sem unicidade natural (múltiplas inconsistências por lote são permitidas — modelo operacional declarado).
- **Dashboard dedicado `GET /api/educacao/educacenso/dashboard`**: DTO novo `EducacaoEducacensoDashboardDto` com métricas reais do tenant — `lotesAtivos` (lotes não terminais), `inconsistenciasAbertas` (inconsistências não terminais), `gravidadeAlta` (subconjunto `tipo='ALTA'`), `eventos` (eventos de censo não deletados, tabela log-like sem filtro de status). Ausência de qualquer uma das 3 tabelas da família lança `RecursoNaoDisponivelException` (regra 13).

Sem novos estados na máquina de estados (S2): indicador/valor/projeção/lote entram `ATIVA` e inconsistência entra `ABERTA`; estado terminal sai da soma vigente e libera a unicidade parcial (lote cancelado pode ser relançado no mesmo ano).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011190000_evo_c1s10_indicadores_educacenso_unicidade.sql` | novo: quatro índices únicos parciais — `ux_indicador_tenant_codigo` sobre `(tenant_id, coalesce(codigo,''))`, `ux_indicador_valor_tenant_ref_ano` sobre `(tenant_id, coalesce(referencia_id,0), coalesce(ano_letivo,0))`, `ux_projecao_matricula_tenant_escola_ano` sobre `(tenant_id, coalesce(escola_id,0), coalesce(ano_letivo,0))` e `ux_educacenso_lote_tenant_ano` sobre `(tenant_id, coalesce(ano_letivo,0))` — todos com `is_deleted=false and ativo=true and [chave] is not null and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` + pós-condição explícita `S10-POSTCOND` (falha nomeada se faltar qualquer um) |
| `database/postgres/migrations/manifest.json` | entrada `20261011190000` registrada (sha256 `0be3cea7b41b02c35dfafb457c03417843c31b7e3b1ff1b990bdf9d4592dd940`, dependência `20261011180000`); 229 entradas |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1 -IncludeDevelopmentSeed` (+`-Verify` → 0) → S10 presente nos 6 consolidados (224 migrations incluídas, 4 excluídas) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | 5 requests novos (`EducacaoIndicadorLancarRequest`, `EducacaoIndicadorValorLancarRequest`, `EducacaoProjecaoMatriculaLancarRequest`, `EducacaoEducacensoLoteLancarRequest`, `EducacaoEducacensoInconsistenciaLancarRequest`) + DTO novo `EducacaoEducacensoDashboardDto(LotesAtivos, InconsistenciasAbertas, GravidadeAlta, Eventos)`; `IEducacaoIndicadoresService` passa a declarar os 6 métodos (`LancarIndicadorAsync`, `LancarIndicadorValorAsync`, `LancarProjecaoMatriculaAsync`, `LancarEducacensoLoteAsync`, `LancarEducacensoInconsistenciaAsync`, `EducacensoDashboardAsync`) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | 8 falhas nomeadas novas: `INDICADOR_CODIGO_EXISTENTE` (409), `INDICADOR_INEXISTENTE` (404), `INDICADOR_NAO_VIGENTE` (409), `VALOR_INDICADOR_EXISTENTE` (409), `PROJECAO_MATRICULA_EXISTENTE` (409), `LOTE_EDUCACENSO_ANO_EXISTENTE` (409), `LOTE_EDUCACENSO_INEXISTENTE` (404), `LOTE_EDUCACENSO_NAO_VIGENTE` (409) |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | 5 métodos de comando (checagens prévias + inserts `ATIVA`/`ABERTA` com auditoria `LANCAR_{INDICADOR,INDICADOR_VALOR,PROJECAO_MATRICULA,LOTE_EDUCACENSO,INCONSISTENCIA_EDUCACENSO}`, com captura 23505 dos índices como backstop) + `EducacensoDashboardAsync` (métricas de censo + checks de existência das 3 tabelas); 3 DTOs privados de leitura com aliases SQL explícitos (padrão Dapper das Sprints 4–9); escopo institucional reutilizado para projeção e para o carregamento de indicador/lote |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | mapeamento HTTP das 8 novas exceções nos catches existentes de criação (404 para as duas `_INEXISTENTE`, 409 para as demais) |
| `src/Sigov.Api/Controllers/EducacaoIndicadoresController.cs` | rotas tipadas `POST indicadores/catalogo/lancar`, `POST indicadores/valores/lancar`, `POST projecoes-matricula/lancar`, `POST educacenso/lotes/lancar`, `POST educacenso/inconsistencias/lancar` + `GET educacenso/dashboard` (sub-path `/lancar` — mesmo padrão ASP0023 das sprints anteriores) |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `IndicadoresEducacenso_Sprint10_CatalogoValorProjecaoLote_UnicidadeEDashboard` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado para 229 (registro da migration S10) (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E14 (21 checks: reset do estado S10 + escola fictícia `c1e26-s10-esc`, indicador lançado/duplicado/backstop, valor lançado/duplicado/backstop/indicador rascunho/indicador inexistente/ano inválido, projeção lançada/duplicada/backstop, lote lançado/duplicado/backstop, inconsistência lançada/lote inexistente/gravidade inválida, dashboard de censo, lote cancelado libera o ano); CX ampliado (deletes tenant 910001 nas 5 tabelas novas + escola + resíduo zero); scan case-insensitive das variáveis novas `$e14*` sem colisões; PARSE_OK |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e** (primeira compilação).
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`S10-POSTCOND|t` — os 4 índices existem); manifest + 6 consolidados sincronizados (regras 6–8).
- Suítes (execução limpa após build): Unit **521/521** · Integration **167/167** · Api **158/158**
  (0 ignorados; total 846; o delta de +1 na Integration é o fato estático novo da Sprint 10).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **150/150 PASS**
  (ts=2026-10-12T01:00:42Z, corrida final/tentativa 6), fixtures fictícios `C1S10-%`/`c1e26-s10-esc` (tenant 910001) aplicados e
  removidos ao final (resíduo zero); os 129 checks efetivos das Sprints 1–9 (E0–E13) revalidados na
  mesma rodada.

## Evidência Tier-2 ao vivo (bloco E14 — indicadores + Educacenso + projeção)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E14.0 | Reset do estado S10 (tabelas indicadores/educacenso do tenant vazias antes do C1) + escola `c1e26-s10-esc` idempotente | exit=0 |
| E14.1 | **Indicador lançado** `matr_por_escola` (código/tipo minúsculos normalizados) | 200; banco codigo\|status\|tipo = MATR_POR_ESCOLA\|ATIVA\|PLANEJAMENTO |
| E14.2 | Segundo indicador com o mesmo código (maiúsculo) | **409 `INDICADOR_CODIGO_EXISTENTE`** (case-insensitive) |
| E14.3 | Insert SQL bruto duplicando o código C1S10-IBS | exit=3 citando `ux_indicador_tenant_codigo` (backstop transacional) |
| E14.4 | **Valor lançado** do indicador ativo (ano 2026, 150) | 200; banco referencia(indicador)\|ano\|tipo\|valor\|status = ⟨id⟩\|2026\|INDICADOR_VALOR\|150.00\|ATIVA |
| E14.5 | Segundo valor do mesmo indicador no mesmo ano | **409 `VALOR_INDICADOR_EXISTENTE`** (um valor vigente por indicador × ano × tenant) |
| E14.6 | Insert SQL bruto duplicando indicador × ano 2027 | exit=3 citando `ux_indicador_valor_tenant_ref_ano` (backstop transacional) |
| E14.7 | Valor em indicador RASCUNHO (fixture SQL) | **409 `INDICADOR_NAO_VIGENTE`** |
| E14.8 | Valor de indicador inexistente (9999999999) | **404 `INDICADOR_INEXISTENTE`** |
| E14.9 | Valor com ano fora da janela (1999) | **400 validação explícita** ("entre 2000 e 2100") |
| E14.10 | **Projeção lançada** da escola no escopo (ano 2026, 30 vagas) | 200; banco escola\|ano\|quantidade\|status = ⟨id⟩\|2026\|30\|ATIVA (planejamento multi-esfera — regra 24) |
| E14.11 | Segunda projeção da mesma escola no mesmo ano | **409 `PROJECAO_MATRICULA_EXISTENTE`** |
| E14.12 | Insert SQL bruto duplicando escola × ano 2029 | exit=3 citando `ux_projecao_matricula_tenant_escola_ano` (backstop transacional) |
| E14.13 | **Lote Educacenso lançado** (ano 2026) | 200; banco ano\|tipo\|status = 2026\|EDUCACENSO\|ATIVA |
| E14.14 | Segundo lote do mesmo ano | **409 `LOTE_EDUCACENSO_ANO_EXISTENTE`** (um lote vigente por ano × tenant) |
| E14.15 | Insert SQL bruto duplicando o ano 2030 | exit=3 citando `ux_educacenso_lote_tenant_ano` (backstop transacional) |
| E14.16 | **Inconsistência lançada** no lote ativo (gravidade/origem minúsculas normalizadas) | 200; banco referencia(lote)\|tipo(gravidade)\|nome(origem)\|status = ⟨id⟩\|ALTA\|MATRICULA\|ABERTA |
| E14.17 | Inconsistência em lote inexistente (9999999999) | **404 `LOTE_EDUCACENSO_INEXISTENTE`** |
| E14.18 | Inconsistência com gravidade fora do conjunto (CRITICA) | **400 validação explícita** (BAIXA/MEDIA/ALTA) |
| E14.19 | **Dashboard de censo** | 200; lotesAtivos/inconsistenciasAbertas/gravidadeAlta/eventos = 1/1/1/0 (medidas reais do tenant após o lançamento da inconsistência ALTA no lote ativo) |
| E14.20 | Lote `CANCELADA` via máquina de estados → relançamento do mesmo ano | 200/200 (estado terminal liberou a unicidade parcial) |
| CX | Limpeza (ampliada com as 5 tabelas novas + escola do tenant 910001) | restantes=0 |

(Blocos E0–E13 das Sprints 1–9 revalidados na mesma rodada: dashboard conjunto (contrato S7),
CSV integral, diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de
versão, escopo institucional, unicidade de matrícula, transferência com linhagem, correção auditada
de frequência, fechamento/reabertura de diário, publicação de histórico escolar versionado, cadastros
base, cardápio, merenda operação, biblioteca circulação, transporte escolar e FDEB + centro de custo —
todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

Duas divergências reais foram descobertas e corrigidas nas corridas oficiais desta sprint — uma no
código (comportamento do comando tipado) e outra no harness (data de referência). Nenhuma foi
mascarada; as corridas que as detectaram permanecem na trilha como evidência:

**1. Normalização do `tipo` do catálogo de indicadores (corrigido na 1ª corrida oficial, 150/150 PASS):**
o eco de banco do check `indicador.lancado-codigo-normalizado` mostrou que a coluna `tipo`
persistia o valor como informado (`planejamento`, minúsculo) embora a intenção declarada fosse
normalizar para maiúsculas — o próprio check passava porque a comparação `-eq` do PowerShell é
case-insensitive por padrão, o que mascarava a divergência. Correção aplicada na mesma janela:

- Repositório: `tipo` do indicador agora é normalizado para maiúsculas no comando tipado
  (`r.Tipo.Trim().ToUpperInvariant()`, com fallback `'INDICADOR'` quando ausente), alinhando o campo
  ao padrão de todos os demais campos normalizados da família (código, fonte, gravidade, origem).
- Driver: a asserção do check foi apertada para `-ceq` (case-sensitive), eliminando a confiança
  falsa; os demais asserts de banco do E14 só comparam literais escritos em maiúsculas pelo próprio
  insert ou valores já normalizados no código, e foram revisados sob o mesmo critério.
- Consequência no pipeline: build Release `-warnaserror` 0w/0e reexecutado e suítes 846/846
  reexecutadas sobre o código final (os apps precisaram ser parados por porta antes do build —
  MSB3026/MSB3027, comportamento conhecido do host).

**2. Data de referência do bloco S3 (ciclo escolar) divergia do relógio do servidor (detectado na 2ª
corrida oficial, 142/142+8 PASS/FAIL — os 8 FAILs todos em S3, todos os 21 do E14 PASS):**
o check `frequencia.criacao-permissao-canonica` retornou 400 e cascata bloqueou 7 checks seguintes
do diário (correção, conferência com pendência, fechamento, reabertura…). Raiz provada por leitura
de código + linha do tempo: o transfer grava a nova matrícula com `data_matricula = current_date`
do **servidor (UTC)**, e a regra de elegibilidade de frequência exige `dataAula >= maior(data_matricula,
data_enturmacao, data_inicio do ano letivo)`; o driver, porém, calculava `$hoje` do **relógio local do
host (America/São_Paulo, UTC-3)** uma única vez no início. A 1ª corrida (20:52 BRT / 23:52 UTC) e a
2ª (21:04 BRT / 00:04 UTC) caíram de cada lado da fronteira: na 2ª, `dataAula=2026-10-11 (BRT)` <
`data_matricula=2026-10-12 (UTC)` → 400 nomeada correta do produto ("Frequência rejeitada: aluno sem
matrícula elegível…"). O comportamento do produto é semântica válida (frequência não pode anteceder a
entrada na turma); o defeito foi do harness. Correção no driver (sem mudança de produto):

- `$hoje` agora vem de `select current_date` do **banco** (relogio autoritativo das janelas de
  elegibilidade), com refresh antes da race, antes da criação da frequência (pós-transfer, o ponto
  crítico) — elimina a divergência na janela 21h–24h BRT sem custo para as demais janelas.
- Evidência do check 7.5 passa a incluir o corpo da resposta (mensagem nomeada), facilitando o
  diagnóstico direto de qualquer falha futura.
- A 3ª corrida (146 PASS/4 FAIL, todos em S4) revelou a **mesma classe** no bloco S4: as datas
  relativas `hoje+1`/`hoje+2` eram derivadas do relógio local do host enquanto `$hoje` já vinha do
  banco — na janela 21h–24h BRT, `hoje_local+1 == hoje_banco`, a segunda chamada de frequência
  duplicou a data e caiu em 400 (cascata: agregação publicando 50% em vez de 66,67% em 3 checks).
  Correção: `dF2/dF3` passam a derivar de `current_date` do banco.
- A 4ª corrida (147 PASS/3 FAIL, todos em S7) definiu o **limite do alinhamento**: o bloco S7 tinha
  sido alinhado ao banco "por classe", mas a camada de biblioteca valida o prazo e grava a
  competência contra o `DateTime.Today` do processo (relógio local), não contra `current_date` —
  dentro da janela 21h–24h BRT o "prazo anterior" (ontem UTC = hoje BRT) passou a ser aceito e a
  competência gravada divergiu da esperada. Correção: as datas do S7 voltaram ao relógio local do
  host (mesma família de relógio que o produto usa nessa camada — é o alinhamento correto); as
  datas dos blocos S3/S4 seguem no `current_date` do banco (camada que compara contra datas de
  banco). Regra resultante declarada no próprio driver: cada bloco usa o relógio da camada que
  ele exerce. Nenhum outro uso do relógio local alimenta dado de negócio além disso — só
  timestamps de log (UTC, decorativos).

**Corridas oficiais desta sprint:** (1) 150/150 PASS — detectora do item 1; (2) 142 PASS/8 FAIL
todos em S3 — detectora do item 2, nenhum em E14; (3) 146 PASS/4 FAIL todos em S4 — detectora da
extensão do item 2, nenhum em E14; (4) 147 PASS/3 FAIL todos em S7 — detectora do limite do
alinhamento (S7 volta ao relógio local do host), nenhum em E14; (5) tentativa **invalidada por
evento de ambiente** — reap do harness matou os apps a ~40s do início e o restante caiu em erro de
conexão (57/93; nada aponta para produto); (6) corrida final sobre o código final (binários
invariantes + driver corrigido) — **evidência oficial desta sprint**. Nenhuma corrida válida com
falha no próprio bloco S10: **E14 passou em todas as corridas válidas**.

## Resíduos declarados (regra 13)

- `referencia_id` do valor guarda o id do indicador e `ano_letivo` guarda o ano (mapeamento
  declarado; as tabelas largas rc50.48 não têm coluna dedicada por entidade de referência neste
  schema — evitar migration além do necessário nesta fatia).
- Inconsistência Educacenso persiste `tipo` = gravidade, `nome` = origem e `descricao` = recomendação
  (mapeamento declarado nas colunas genéricas da tabela larga).
- Valor de indicador aceita apenas `valor > 0` (400 para zero/negativo) — indicador de taxa com
  decimal (ex.: 0,15) é suportado pelo `decimal`; valores negativos não são modelados nesta fatia.
- Indicador/valor/projeção/lote entram direto em `ATIVA` no comando tipado (simplificação
  declarada, padrão S8/S9); `RASCUNHO → ATIVA` segue disponível via máquina de estados nas rotas
  genéricas da família. Inconsistência entra `ABERTA`.
- Dashboard de censo conta `eventos` sem filtro de status (tabela log-like `educacao_educacenso_evento`);
  os demais contadores usam o filtro vigente (não terminais).
- Projeção de matrícula usa o escopo institucional do S2 (escola deve pertencer à entidade do
  contexto); tenancy é a fronteira dura da unicidade — a mesma escola em tenets diferentes tem
  projeções independentes.
- Sem comando tipado ainda para lançamentos/rateio de custo e para avaliação de magistério
  (próxima fatia S11 — fechamento do ciclo); essas tabelas seguem no CRUD genérico da família —
  declarado.
- Herdadas das sprints anteriores (não reintroduzidas): `validate-script-completop` falha em seed
  pré-existente `admin@sigov.local` (já em HEAD); `rc50_25` órfã permanece intocada (regra 8); RAM
  local cronicamente esgotada neste host — apps mantidos em shells dedicados + corrida oficial atrás
  de sonda de readiness (mesma política operacional das Sprints 8–9). A 5ª tentativa foi **invalidada
  por evento de ambiente, não de produto**: os shells dos apps foram reapados pelo harness no meio da
  corrida (~40s, bloco S4) e todos os checks seguintes retornaram erro de conexão (57/93); os apps
  voltaram como processos destacados (`Start-Process`, fora do job object do shell trackeado) e a
  corrida seguinte é a final oficial.
