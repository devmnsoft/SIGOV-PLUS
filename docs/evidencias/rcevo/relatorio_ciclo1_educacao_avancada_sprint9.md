# Relatório Ciclo 1 · Sprint 9 — Educação Avançada (FDEB: parâmetro/receita/aplicação + centro de custo + dashboard de conformidade)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **vertical FDEB de conformidade fiscal + centros de custo** multi-esfera (regra 24) sobre as tabelas `sigov.educacao_fundeb_*`/`sigov.educacao_custo_*` criadas na RC50.48: quatro comandos tipados novos e um dashboard de conformidade dedicado:

- **Comando tipado `POST /api/educacao/fundeb/parametros/lancar`** com `{codigo, valor}`:
  - Códigos normalizados para maiúsculas (comparação case-insensitive declarada).
  - **Um código vigente por tenant**: repetição → **409 `FUNDEB_PARAMETRO_CODIGO_EXISTENTE`**, com o índice único parcial `ux_fundeb_parametro_tenant_codigo` como backstop transacional.
  - A **taxa mínima de aplicação** do Fundeb (LDB art. 70 e equivalentes de cada esfera) é parametrizada por esfera de governo e lida do parâmetro `TAXA_MINIMA_APLICACAO` do banco — federal/estadual/municipal cada qual define a sua; **nunca hardcoded** (regra 24).
- **Comando tipado `POST /api/educacao/fundeb/receitas/lancar`** com `{competencia, fonte, valor}`:
  - `fonte` obrigatória (**400 validação explícita**) — identifica a origem do recurso (FPE/FNDE/receita própria…); persistida na coluna `tipo` das tabelas largas rc50.48 (mapeamento declarado), normalizada para maiúsculas.
  - `competencia` como `DateOnly` válida (≥ 2000-01-01, senão **400 validação explícita**); `valor > 0` (senão 400).
  - **Uma receita vigente por competência × fonte × tenant**: repetição → **409 `FUNDEB_RECEITA_DUPLICADA`**, com o índice único parcial `ux_fundeb_receita_tenant_competencia_fonte` como backstop transacional.
- **Comando tipado `POST /api/educacao/fundeb/aplicacoes/lancar`** com `{centroCustoId, valor}`:
  - Centro inexistente/inativo → **404 `CUSTO_CENTRO_INEXISTENTE`**; centro não vigente (ex.: `RASCUNHO`, `CANCELADA`) → **409 `CUSTO_CENTRO_NAO_VIGENTE`** (escopo institucional reutilizado via helper do S2).
  - `valor > 0` (senão **400 validação explícita**). Sem unicidade natural: múltiplas aplicações por centro são permitidas (modelo operacional declarado).
- **Comando tipado `POST /api/educacao/custos/centros/lancar`** com `{codigo, nome}`:
  - **Um código vigente por tenant**: repetição → **409 `CUSTO_CENTRO_CODIGO_EXISTENTE`**, com o índice único parcial `ux_custo_centro_tenant_codigo` como backstop transacional; centro criado direto `ATIVA` (`tipo='CUSTO_EDUCACAO'`) com auditoria `LANCAR_CENTRO_CUSTO`.
- **Dashboard dedicado `GET /api/educacao/indicadores/dashboard`**: migrou do genérico para o DTO pré-existente `EducacaoIndicadoresDashboardDto` (scaffolding órfão da RC50) estendido com métricas reais de conformidade FDEB sobre dados do tenant — `receitasFundeb`, `aplicacoesFundeb`, `taxaAplicacao` (round 4 casas), `taxaMinima` (`decimal?` lida do parâmetro `TAXA_MINIMA_APLICACAO`; null quando a esfera não parametrizou), `emConformidade` (`minima > 0 && receitas > 0 && taxa >= minima` — **falsa explicitamente sem parâmetro ou sem receita vigente**, sem sucesso simulado, regra 13), `custoPorAluno` (soma dos lançamentos de custo ÷ matrículas vigentes ATIVA/CONFIRMADA, round 2 casas; 0 quando não há matrículas — sem divisão por zero) e `inconsistencias` (inconsistências vigentes do educacenso). Ausência de qualquer uma das 5 tabelas da família lança `RecursoNaoDisponivelException` (regra 13).

Sem novos estados na máquina de estados (S2): parâmetro/receita/aplicação/centro entram `ATIVA` e saem pela família terminal já existente; estado terminal sai da soma vigente e libera a unicidade parcial (receita cancelada pode ser relançada na mesma competência/fonte; centro cancelado bloqueia novas aplicações).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011180000_evo_c1s9_fundeb_custos_unicidade.sql` | novo: três índices únicos parciais — `ux_fundeb_parametro_tenant_codigo` sobre `(tenant_id, coalesce(codigo,''))`, `ux_fundeb_receita_tenant_competencia_fonte` sobre `(tenant_id, coalesce(competencia,'2999-12-31'), coalesce(tipo,''))` e `ux_custo_centro_tenant_codigo` sobre `(tenant_id, coalesce(codigo,''))` — todos com `is_deleted=false and ativo=true and codigo/competencia/tipo is not null and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` + pós-condição explícita `S9-POSTCOND` (falha nomeada se faltar qualquer um) |
| `database/postgres/migrations/manifest.json` | entrada `20261011180000` registrada (sha256 `b39513596b1bae11740a56c829e3d3db2ae5f4d2fdb280dd7e397487ac0b8011`, dependência `20261011170000`); 228 entradas |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1 -IncludeDevelopmentSeed` (+`-Verify` → 0) → S9 presente nos 6 consolidados |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | `EducacaoIndicadoresDashboardDto` (scaffolding órfão) estendido in-place para o contrato S9 (7 campos de conformidade); 4 requests novos (`…LancarParametroRequest`, `…LancarReceitaRequest` com `DateOnly Competencia`, `…LancarAplicacaoRequest`, `…LancarCentroCustoRequest`); `IEducacaoIndicadoresService` passa a declarar os 5 métodos (`LancarParametroAsync`, `LancarReceitaAsync`, `LancarAplicacaoAsync`, `LancarCentroCustoAsync`, `IndicadoresDashboardAsync`) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | 5 falhas nomeadas novas: `FUNDEB_PARAMETRO_CODIGO_EXISTENTE` (409), `FUNDEB_RECEITA_DUPLICADA` (409), `CUSTO_CENTRO_INEXISTENTE` (404), `CUSTO_CENTRO_NAO_VIGENTE` (409), `CUSTO_CENTRO_CODIGO_EXISTENTE` (409) |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | 5 métodos da família indicadores (checagens prévias + inserts `ATIVA` com auditoria `LANCAR_{PARAMETRO,RECEITA,APLICACAO,CENTRO_CUSTO}` na mesma transação, com captura 23505 dos índices como backstop); `IndicadoresDashboardAsync` (métricas de conformidade + checks de existência das 5 tabelas); helper privado de escopo institucional reutilizado para o centro de custo; 2 DTOs privados de leitura com aliases SQL explícitos (padrão Dapper das Sprints 4–8) |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | mapeamento HTTP das 5 novas exceções nos catches existentes de criação |
| `src/Sigov.Api/Controllers/EducacaoIndicadoresController.cs` | `GET indicadores/dashboard` agora retorna o DTO dedicado; rotas tipadas `POST fundeb/parametros/lancar`, `POST fundeb/receitas/lancar`, `POST fundeb/aplicacoes/lancar`, `POST custos/centros/lancar` (sub-path `/lancar` — mesmo padrão ASP0023 das sprints anteriores) |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `FundebCustos_Sprint9_ParametroReceitaAplicacao_UnicidadeEDashboard` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado para 228 (registro da migration S9) (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E13 (20 checks: reset do estado S9, parâmetro lançado/duplicado/backstop, receita lançada/duplicada/backstop/sem fonte/competência inválida, centro lançado/duplicado/backstop, aplicação em centro RASCUNHO/inexistente/ativa/valor inválido, dashboard de conformidade acima do mínimo, receita cancelada sai da soma + libera a unicidade, centro cancelado bloqueia aplicações); CX ampliado (função delete tenant 910001 nas 5 tabelas fundeb/custo + resíduo zero); scan case-insensitive das variáveis novas `$e13*` sem colisões; PARSE_OK |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e** (primeira compilação).
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`S9-POSTCOND|t` — os 3 índices existem); manifest + 6 consolidados sincronizados (regras 6–8).
- Suítes (execução limpa após build): Unit **521/521** · Integration **166/166** · Api **158/158**
  (0 ignorados; total 845; o delta de +1 na Integration é o fato estático novo da Sprint 9).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **129/129 PASS**
  (ts=2026-10-11T21:33:56Z), fixtures fictícios `C1S9-%`/`TAXA_MINIMA_APLICACAO` (tenant 910001) aplicados e
  removidos ao final (resíduo zero); os 109 checks efetivos das Sprints 1–8 (E0–E12) revalidados na
  mesma rodada.

## Evidência Tier-2 ao vivo (bloco E13 — FDEB + centro de custo)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E13.0 | Reset do estado S9 (tabelas fundeb/custo do tenant vazias antes do C1) | exit=0 |
| E13.1 | **Parâmetro lançado** `taxa_minima_aplicacao` 0.5 (minúsculo normalizado) | 200; banco codigo\|valor\|status = TAXA_MINIMA_APLICACAO\|0.50\|ATIVA (taxa mínima da esfera vem do banco, regra 24) |
| E13.2 | Segundo parâmetro com o mesmo código (maiúsculo) | **409 `FUNDEB_PARAMETRO_CODIGO_EXISTENTE`** (case-insensitive) |
| E13.3 | Insert SQL bruto duplicando o código C1S9-PBS | exit=3 citando `ux_fundeb_parametro_tenant_codigo` (backstop transacional) |
| E13.4 | **Receita lançada** competência 2026-01-15 + fonte `fpe` 10000 | 200; banco competencia\|tipo\|valor\|status = 2026-01-15\|FPE\|10000.00\|ATIVA |
| E13.5 | Receita duplicada (mesma competência/fonte, case-insensitive) | **409 `FUNDEB_RECEITA_DUPLICADA`** |
| E13.6 | Insert SQL bruto duplicando competência/fonte | exit=3 citando `ux_fundeb_receita_tenant_competencia_fonte` (backstop transacional) |
| E13.7 | Receita sem fonte | **400 validação explícita** ("exige fonte") |
| E13.8 | Receita com competência inválida (0001-01-01) | **400 validação explícita** de data |
| E13.9 | **Centro de custo lançado** `c1s9-cc1` (minúsculo normalizado) | 200; banco codigo\|status\|tipo = C1S9-CC1\|ATIVA\|CUSTO_EDUCACAO |
| E13.10 | Centro duplicado (mesmo código) | **409 `CUSTO_CENTRO_CODIGO_EXISTENTE`** |
| E13.11 | Insert SQL bruto duplicando o código C1S9-CBS | exit=3 citando `ux_custo_centro_tenant_codigo` (backstop transacional) |
| E13.12 | Aplicação em centro RASCUNHO | **409 `CUSTO_CENTRO_NAO_VIGENTE`** |
| E13.13 | Aplicação em centro inexistente (9999999999) | **404 `CUSTO_CENTRO_INEXISTENTE`** |
| E13.14 | **Aplicação lançada** em centro ativo (6000) | 200; banco centro_custo_id\|valor\|status = 1\|6000.00\|ATIVA |
| E13.15 | Aplicação com valor 0 | **400 validação explícita** ("positivo") |
| E13.16 | **Dashboard de conformidade** acima do mínimo | 200; receitasFundeb/aplicacoesFundeb/taxaAplicacao/taxaMinima/emConformidade/custoPorAluno/inconsistencias = 10000/6000/0.6/0.5/true/0/0 |
| E13.17 | Receita `CANCELADA` via máquina de estados → dashboard | 200; receitas/taxa/emConformidade = 0/0/false (aplicações permanecem 6000 — terminal sai da soma vigente) |
| E13.18 | Relançamento da mesma competência/fonte após o cancelamento | 200 (estado terminal liberou a unicidade parcial) |
| E13.19 | Centro `CANCELADA` via máquina de estados → nova aplicação nele | 200/409 `CUSTO_CENTRO_NAO_VIGENTE` (terminal bloqueia operação) |
| CX | Limpeza (ampliada com as 5 tabelas fundeb/custo do tenant 910001) | restantes=0 |

(Blocos E0–E12 das Sprints 1–8 revalidados na mesma rodada: dashboard conjunto (contrato S7),
CSV integral, diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de
versão, escopo institucional, unicidade de matrícula, transferência com linhagem, correção auditada
de frequência, fechamento/reabertura de diário, publicação de histórico escolar versionado, cadastros
base, cardápio, merenda operação, biblioteca circulação e transporte escolar — todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

Nenhum defeito de código foi descoberto nas corridas ao vivo: o bloco E13 (20 checks) passou
integralmente na primeira corrida oficial válida, e os 109 checks efetivos das Sprints 1–8 revalidaram
na mesma rodada (129/129). Sem corridas do driver descartadas nesta sprint — os apps foram mantidos em
shells dedicados atrás de sonda de readiness (política da Sprint 8), e o único ajuste operacional desta
janela (cast de status HTTP no wrapper de readiness, fora do repositório) ocorreu antes da primeira
tentativa do driver, sem produzir corrida descartada.

## Resíduos declarados (regra 13)

- `fonte` da receita persiste na coluna `tipo` das tabelas largas rc50.48 (mapeamento declarado; não
  existe coluna dedicada por fonte neste schema — evitar migration além do necessário nesta fatia).
- Parâmetro FDEB usa `nome = codigo` para exibição em listagens (o comando tipado não recebe nome);
  aceita `valor ≥ 0` (taxa de 0% é uma declaração legítima da esfera; valor negativo é 400).
- Aplicação não tem unicidade natural (múltiplas aplicações por centro são permitidas); a integridade
  vem do centro ter que existir/ser vigente no momento do lançamento.
- `custoPorAluno` usa matrículas vigentes (ATIVA/CONFIRMADA) de `sigov.matricula`; com zero
  matrículas retorna 0 (sem divisão por zero); lançamentos de custo fora do comando tipado
  (CRUD genérico) ainda entram na soma.
- Sem comando tipado ainda para lançamentos/rateio de custo, indicadores específicos e educacenso
  (próximas fatias S10+); essas tabelas seguem no CRUD genérico da família — declarado.
- `emConformidade=false` quando a esfera não parametrizou `TAXA_MINIMA_APLICACAO` ou quando não há
  receita vigente — falha explicita, sem sucesso simulado (regra 13).
- Centros entram direto em `ATIVA` no comando tipado (simplificação declarada, padrão S8);
  `RASCUNHO → ATIVA` segue disponível via máquina de estados nas rotas genéricas da família.
- Herdadas das sprints anteriores (não reintroduzidas): `validate-script-completop` falha em seed
  pré-existente `admin@sigov.local` (já em HEAD); `rc50_25` órfã permanece intocada (regra 8); RAM
  local cronicamente esgotada neste host — apps mantidos em shells dedicados + corrida oficial atrás
  de sonda de readiness (mesma política operacional da Sprint 8).
