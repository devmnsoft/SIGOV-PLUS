# Relatório Ciclo 1 — Educação Avançada, Sprint 1 (correções comprovadas) + prova Tier‑2

Data: 2026-10-10 (UTC). Base: HEAD `f6e4aa4c` (worktree `swift-circuit`; merge da ponta de `origin/main` `aaec9515` com a linha local P0/Gate B). Banco: `p1_dev` @ PostgreSQL 16 (container oficial `sigov-postgres`, forwarder socat na porta 5433). Apps: Web :5099 / API :5100 locais, `MigrationMode=Disabled`.

## Features desta sprint

Escopo: as 7 correções comprovadas de Educação Avançada — implementada e provada ao vivo a fatia #1/#2/#3/#5/#7; **#4** (máquinas de estado por recurso) e **#6** (escopo institucional completo) são residuais declarados para a Sprint 2.

| # | Correção | Onde | Comprovado ao vivo (Tier‑2) |
|---|---|---|---|
| 1 | Dashboard sobre o conjunto completo (`count(1) filter(...)` + 6 recentes num `QueryMultipleAsync` único) | `EducacaoAvancadaRepository.DashboardAsync` | `total=120 ativos=60 pendentes=20 alertas=20 recentes=6` (código antigo veria 0/0/0 sobre os 6 recentes) |
| 2 | Exportação CSV integral (sem teto de 100 linhas) | `ExportarCsvAsync` | 122 linhas físicas (cabeçalho + 120 dados + 1 extra por quebra embutida em campo aspas); fixo antigo truncava em 101 |
| 3 | Escape/proteção de fórmula, aspas RFC, BOM, `InvariantCulture` | helper novo `EducacaoCsv.Campo` + `ExportarCsvAsync` | célula de fórmula íntegra no byte do download (`'` prefixo + aspas dobradas), BOM UTF‑8 EF BB BF, `linha1<LF>linha2` preservada |
| 5 | Schema ausente ⇒ diagnóstico operacional explícito (não lista vazia silenciosa) | `PagedResult<T>.Indisponivel(page,size,diagnostic)` | probe de rename transitório de `educacao_merenda_requisicao`: HTTP 200/items=0 com diagnostic "Recurso 'sigov.educacao_merenda_requisicao' indisponível nesta instância: schema ausente…" e restauração sem perda de dados |
| 7 | Auditoria append‑only de transição de status (de/para/motivo) | `AlterarStatusAsync` | ATIVA→ATIVA→CANCELADA gravou 2 entradas `{"acao":"STATUS","de":...,"para":...,"motivo":...}` preservando a anterior |

Regras atendidas: PG16/Dapper/Clean Architecture (.NET 10 / C# 14, sem EF); sem migration nesta sprint (schema existente reutilizado — regras 6–8 intactas); autorização server‑side preservada (`[Authorize]` na base dos controllers, bearer 64 chars mintado via Web); tenant‑scoping em todas as leituras/gravações; seed 100% fictício, idempotente, sem senha/token/dado pessoal real (regras 9–10); falha ausente de schema vira diagnóstico, nunca sucesso simulado (regra 13); apenas ampliação de classes de teste existentes (regra 14).

## Defeitos runtime descobertos na prova ao vivo (corrigidos em código, sem máscara)

1. Dapper não materializa `IDictionary<string,object>` em `QueryMultiple` ("Invalid type owner for DynamicMethod") → leitura tipada com `record EducacaoTotais(long Total,long Ativos,long Pendentes,long Alertas)` no dashboard.
2. Alias SQL não-aspirado chega minúsculo no reader (`createdat`) e quebrava a materialização do `EducacaoAvancadaRegistroDto` → `created_at as "CreatedAt"` aspirado nos três SELECTs (Listar/Dashboard/Export).
3. Expectativa E4 do driver assumia que a linha testada nasceu RASCUNHO; a linha 1 do fixture nasce ATIVA → cadeia real ATIVA→ATIVA→ATIVA→CANCELADA; regex/expectativas corrigidas no script (erro do teste, não defeito do produto).

## Arquivos

- `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` — dashboard, CSV integral, diagnóstico de schema, auditoria append‑only + defeitos 1–2 acima
- `src/Sigov.Application/Educacao/Avancada/EducacaoCsv.cs` — novo helper (guarda de fórmula `=+-@\t\r` com prefixo `'`, aspas RFC, InvariantCulture)
- `src/Sigov.Application/Common/PagedResult.cs` — propriedade opcional `Diagnostic` + fábrica `Indisponivel(page,size,diagnostic)`
- `tests/Sigov.UnitTests/PagedResultTests.cs` (+7 fatos) e `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` (+1 fato estático) — classes existentes ampliadas, sem classe nova (regra 14)
- `scripts/c1_tier2_e2e.ps1` — driver reproduzível (login→bearer, aplica fixture, E1–E4, cleanup; artefatos em `artifacts/c1_tier2_e2e/`)
- `database/postgres/seeds/homologacao/20261010_c1_educacao_avancada_tier2_fixture.sql` — fixture fictício idempotente (marcador `c1e26-%`, tenant 910001, 120 linhas: 60 ATIVA / 20 RASCUNHO / 20 ATRASADO / 20 ENCERRADA + linhas de conteúdo fórmula/newline)
- `docs/evidencias/rcevo/relatorio_ciclo0_migration_audit_apply_migrations.md` — relatório do Ciclo 0 entregue na mesma base

## Comandos

```
dotnet build sigov.tests.sln -c Release -warnAsError          # 0w/0e
dotnet test tests/Sigov.UnitTests -c Release --no-build       # 515/515
dotnet test tests/Sigov.IntegrationTests -c Release           # 159/159
dotnet test tests/Sigov.ApiTests -c Release                   # 158/158
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\c1_tier2_e2e.ps1   # 12/12 PASS
```

## Evidência Tier‑2

Rodada final ts=2026-10-10T12:23Z — **PASS=12 FAIL=0**:

1. `env.web-up` GET /Auth/Login → 200
2. `env.api-up` API anônima → 401 (esperado)
3. `auth.login-web` hom.a.admin → 302|MinhaCentral
4. `auth.api-token-mint` ApiToken len=64
5. `fixture.aplicado` exit=0
6. `dashboard.conjunto-completo` 200 — total=120 ativos=60 pendentes=20 alertas=20 recentes=6
7. `csv.export-integral-bom-escape` 122 linhas, bom_utf8=True, formula_escapada=True, newline_preservado=True
8. `probe.tabela-vazia-prerename` rows=0 antes do rename
9. `listar.diagnostic-schema-ausente` 200/items=0 + diagnostic "schema ausente"
10. `probe.tabela-restaurada` rename de volta ok, rows=0 depois
11. `status.auditoria-de-para-motivo-append` 2 POSTs 200; auditoria com 2 STATUS preservando a anterior (amostra no log)
12. `fixture.removido` resíduo 0

Rodada anterior (mesmo dia, ~12:1xZ): 9/12 — as 3 falhas eram exatamente os defeitos 1, 2 e a expectativa 3; corrigidos, a rodada final passou 12/12. Logs: `Temp\opencode\c1_{web,api}*.log`; artefatos: `artifacts/c1_tier2_e2e/`.

## Falhas / blockers

- Nenhum blocker aberto para os 5 itens do escopo — todos com prova ao vivo.
- Residuais (Sprint 2): #4 máquinas de estado por recurso (hoje toda transição é aceita; cancelamento já exige justificativa) e #6 escopo institucional completo (tenant isolado comprovado; entidade/unidade gestora/exercício ainda parcial).
- Carreados do Ciclo 0: C0‑R1 (janitor >45 min; mudança adiada para RC própria), C0‑F1 (limpeza da sonda `evo_c_probe_aplicada` pendente com dono do RC) e L1 (ciclos completos clean‑install/upgrade/reapply/recovery em banco descartável com o runner atual — o audit foi estático + provas pontuais e não substitui esse gate).

## Riscos

- `p1_dev` é banco dev/homologação: a prova usa seus seeds existentes + fixture removível; nada de produção tocado (migrations nunca contra produção).
- Paridade alias SQL aspirado ⇄ propriedade do DTO (exigência do Dapper com Npgsql 10) fica protegida pelos fatos estáticos do smoke test e pelo E2E — regressão futura aparece verde→vermelho nos dois níveis.
- Rate limit de login (~10/min/IP) pode exigir backoff em reexecuções densas do driver (já tratado no script, retry com backoff).

## Atualização de matriz/backlog

`docs/execucao/ULTIMA_EXECUCAO.md` (entrada 2026-10-10 Ciclo 0+1), `docs/execucao/MATRIZ_FLUXOS_VERTICAIS.md` (parágrafo normativo — trilha RCEVO), `docs/execucao/BACKLOG_EXECUTAVEL.md` (item 7a + próximo item exato).

## Próxima sprint (Ciclo 1, Sprint 2)

1. **#4** — máquinas de estado por recurso: transições permitidas por família, recusa nomeada para transição inválida, guarda concorrente por versão; fatos nas classes existentes + prova ao vivo no Tier‑2.
2. **#6** — escopo institucional completo: entidade/unidade gestora/unidade executora/exercício como filtro e escrita obrigatória, multi‑esfera (regras 21–24), sem comportamento municipal hardcoded.
3. Em seguida: demais ciclos RCEVO na ordem registrada. Na matriz vertical, Gate C (Indústria OP) segue como próxima vertical. GED por último.
