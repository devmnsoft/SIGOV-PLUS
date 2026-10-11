# Relatório Ciclo 1 · Sprint 3 — Educação Avançada (ciclo escolar: unicidade por oferta + correção auditada de frequência)

Data: 2026-10-11 · Commit validado: pendente de preenchimento após commit · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

Ciclo escolar completo no núcleo escolar (`EducacaoRepository`), multi-esfera (regra 24):

- **Unicidade de matrícula vigente por aluno × turma (oferta)**: índice único parcial
  `ux_matricula_tenant_aluno_turma_vigencia` sobre `(tenant_id, entidade_id, aluno_id, turma_id)`
  com `status in ('ATIVA','CONFIRMADA')`; `turma_id NULL` não conflita (matrículas ainda sem
  turma nunca se bloqueiam). Grupos duplicados pré-existentes **falham explicitamente** na
  migration (regra 13) em vez de mascaramento.
- **Transferência com histórico e linhagem**: origem vira `TRANSFERIDA` com motivo/observação;
  a nova matrícula nasce `ATIVA` com `origem = TRANSFERENCIA:<id original>`; vagas das duas
  turmas movimentadas por update-first serializado (sem oversell); 2 eventos de auditoria em
  `sigov.educacao_evento` (`MatriculaTransferida`, `MatriculaCriadaPorTransferencia`).
- **Correção auditada de frequência**: `POST /api/educacao/frequencias/{id}/correcao`
  grava `situacao/presente/justificativa/updated_by` sob lock `for update` e audita o evento
  `FrequenciaCorrigida` com `de/para/justificativa`.
- **Fechamento/reabertura do período bloqueia correções**: com `educacao_diario_classe.status='FECHADO'`
  a correção falha nomeada ("Correção bloqueada: o diário de classe da turma está FECHADO;
  reabra o período antes de corrigir"); reabertura exige justificativa e grava histórico
  (`REABERTO` + linha em `educacao_diario_historico`); após reabrir, correção volta a funcionar.
- **Corretiva de schema (regra 8)**: `diario_frequencia.situacao` — a migration `rc50_25`
  (`20260813160000`) é **órfã do manifest.json** (nunca rodou no runner); sem a coluna, o
  contrato C# de frequência falharia sempre em runtime (regra 13). A migration nova adiciona
  a coluna idempotentemente, faz backfill a partir de `presente` e impõe o CHECK canônico
  (`PRESENTE/FALTA/JUSTIFICADA/ABONADA`). Remove também o índice estreito legado
  `ux_diario_frequencia_turma_aluno_data` (sem `componente_curricular`, proibia registrar mais
  de um componente por dia no mesmo aluno/turma, contradizendo o índice canônico com componente).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011120000_evo_c1s3_educacao_matricula_unicidade_oferta.sql` | novo: migration corretiva idempotente (unicidade por oferta + `situacao`/CHECK em `diario_frequencia` + remoção do índice estreito legado) |
| `database/postgres/migrations/manifest.json` | entrada `20261011120000` registrada (sha256 `f98828958ee6710f78d73ea539dbdfbf27e6410017911f66d935572f0c41a41f`); 221 migrations |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados por migration (regra 7) via `generate-script-completop.ps1 -IncludeDevelopmentSeed -Verify` → exit 0 |
| `src/Sigov.Application/Educacao/EducacaoContracts.cs` | `FrequenciaCorrecaoRequest(Status, Justificativa)` + métodos de serviço no contrato; `IMatriculaService.TransferirAsync` passa a retornar `Result<long>` (id da nova matrícula) |
| `src/Sigov.Infrastructure/Educacao/EducacaoRepository.cs` | `CorrigirFrequenciaAsync` (lock `for update`, bloqueio quando diário FECHADO, atualização auditada + evento `FrequenciaCorrigida` em `sigov.educacao_evento`); `EnturmarAsync`/`TransferirAsync` com update-first + eventos de linhagem |
| `src/Sigov.Application/Educacao/EducacaoServices.cs` | `ExecutarCorrecaoFrequenciaAsync` (guarda canônica `frequencia/justificar`); **correção**: `IFrequenciaService.CriarAsync` validava permissão inexistente `diario_frequencia/criar` (sempre 403) → novo `CriarFrequenciaAsync` com `frequencia/criar`; `TransferirAsync` expõe o id da nova matrícula |
| `src/Sigov.Api/Controllers/EducacaoControllers.cs` | `POST /api/educacao/frequencias/{id:long}/correcao` + mapeamento `FromResult` de falhas nomeadas (400; 403 p/ permissão; 404 p/ registro ausente); `transferir` responde `ApiResponse<long>` |
| `src/Sigov.Infrastructure/Educacao/Bloco3/EducacaoBloco3Repository.cs` | **correção**: `Parametros` pré-serializa coleções de POCO (ex.: `Alunos`) que o jsonb writer do Npgsql não suporta como parâmetro |
| `src/Sigov.Application/Educacao/Bloco3/EducacaoBloco3Contracts.cs` | **correção**: `EducacaoDiarioFechamentoDto.CreatedAt` → `DateTime` (timestamptz via Npgsql materializa como `DateTime`; `DateTimeOffset` quebrava o ctor matching do Dapper no `historico-fechamentos`) |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `EducacaoCicloEscolar_Sprint3_UnicidadeOferta_E_CorrecaoAuditadaDeFrequencia` (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E7 (race da última vaga, prova direta do índice, transferência com linhagem, criação/correção auditada, diário completo → fechar/bloquear/reabrir) + CX ampliado (cascata do diário via temp table) |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e**.
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira;
  manifest + 6 consolidados sincronizados (regras 6–8).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **41/41 PASS** (ts=2026-10-11T13:07:53Z),
  fixtures fictícios `c1e26-%`/`C1S3-%` (tenant 910001) aplicados e removidos ao final (resíduo zero);
  os 25 checks da Sprint 2 (E0–E6) revalidados na mesma rodada.
- Suítes (reexecução limpa pós-run E2E, timeouts estendidos): Unit **521/521** · Integration
  **160/160** · Api **158/158** (0 ignorados; total 839/839). O count do manifest no teste existente
  `SaasComercialTests.Manifest_tem_217_migrations_registradas_para_a_rc_saas_aut` foi atualizado de
  220→221 entradas para registrar esta migration (classe existente, regra 14).

## Evidência Tier-2 ao vivo (bloco E7 — ciclo escolar)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E7.0 | Fixture do contexto e da oferta fictícia (escola+ano+2 turmas+3 alunos+professor MATEMATICA+matrícula enturmada+2 sem turma) | entidade efetiva=1 (contexto autenticado; multi-esfera, sem município hardcoded) |
| E7.1 | **Corrida pela última vaga** (capacidade 2, 1 vaga livre; 2 POST `enturmar` em paralelo) | exatamente **1×200 + 1×400** (update-first com `for update` serializa; sem oversell) |
| E7.2 | Perdedor da corrida | permanece sem turma (`turma_id IS NULL`), matrícula `ATIVA` intacta |
| E7.3 | **Prova direta do índice**: INSERT SQL de 2ª matrícula ATIVA do mesmo aluno/turma (`ativo=false` isola o índice de oferta do índice por ano) | violação explícita em `ux_matricula_tenant_aluno_turma_vigencia` |
| E7.4 | Transferência com histórico/linhagem | banco: `TRANSFERIDA|motivo@@@ATIVA|TRANSFERENCIA:<origem>|<novaTurma>@@@vagas A 2→1, B 0→1@@@2 eventos de auditoria` |
| E7.5 | Transferência para a mesma turma | **400 "Transferência rejeitada"** (falha nomeada, não 500) |
| E7.6 | Criação de frequência (permissão canônica `frequencia/criar`) | 200, id retornado (guarda corrigida; antes sempre 403) |
| E7.7 | Correção auditada PRESENTE→JUSTIFICADA | banco `JUSTIFICADA|f|justificativa|usuário`; evento `FrequenciaCorrigida` com `"de":"PRESENTE","para":"JUSTIFICADA"` |
| E7.8 | Diário de classe completo (aula + conteúdo + frequência do dia) e conferência | `podeFechar=true pendencias=0` + token de conferência |
| E7.9 | Fechamento com token | status banco=`FECHADO` |
| E7.10 | Correção com diário FECHADO | **400 "Correção bloqueada…FECHADO…reabra"** (regra 13) |
| E7.11 | Reabertura com justificativa | `REABERTO|1` (status + linha no histórico) |
| E7.12 | Correção pós-reabertura | 200; banco `PRESENTE|t` |
| E7.13 | Histórico de fechamentos | ≥1 entrada registrada (fechamento com justificativa e versão) |
| CX | Limpeza do fixture | `restantes=0` |

(Blocos E0–E6 da Sprint 2 revalidados na mesma rodada: dashboard conjunto completo, CSV integral
com BOM/escape/proteção de fórmula, diagnóstico de schema ausente, auditoria append-only de
transição, máquina de estados + guarda de versão e escopo institucional — todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

1. `IFrequenciaService.CriarAsync` validava a permissão `diario_frequencia/criar`, que **não existe**
   no catálogo; o avaliador de autorização casa o recurso por fuzzy mas não faz ponte entre os dois
   nomes → toda criação de frequência caía em 403 até para perfil com `frequencia/criar` → guarda
   trocada para a permissão real `frequencia/criar`.
2. `TransferirAsync` (matrícula) descartava o id retornado pelo repositório (`data:{}` na resposta);
   a linhagem exigia que o cliente soubesse a nova matrícula → contrato agora `Result<long>` e a
   rota responde `ApiResponse<long>`.
3. Lançamento de frequência do diário (Bloco3) 500: `Parametros` passava cada propriedade do
   request como parâmetro Dapper, incluindo `Alunos` (lista de POCO), que o jsonb writer do Npgsql
   não suporta escrever → 500 em todo lançamento (cascateava em conferência/fechamento) →
   coleções pré-serializadas para texto (o SQL consome só `cast(@Dados as jsonb)`).
4. `historico-fechamentos` 500: Dapper não materializa o record com parâmetro `DateTimeOffset`
   contra coluna `timestamptz` (materializada como `DateTime`) → DTO alinhou-se à convenção dos
   demais DTOs Bloco3 (`DateTime CreatedAt`).
5. Driver Tier-2 (artefatos de teste, declarados): PS 5.1 não faz quoting de `Start-Process
   -ArgumentList` com espaços (bearer se partia) → race trocado para `HttpClient.SendAsync`;
   CTE `WITH` do bloco CX só se ligava ao primeiro DELETE (Postgres não propaga CTE entre sentenças)
   → temp table; `$ErrorActionPreference='Stop'` + `2>&1` transformava o stderr do psql em exceção
   onde o texto de erro é o resultado esperado → captura em `Continue` dentro de `Get-Psql`;
   bodies HTTP lidos em UTF-8 (acento em falhas nomeadas) e booleanos do psql PG16 chegam como
   `true/false` (não `t/f`).

## Resíduos declarados (regra 13)

- `validate-script-completop` falha em seed pré-existente `admin@sigov.local` (2 ocorrências já em
  HEAD; não introduzidas nesta sprint) — declarado, não mascarado.
- `rc50_25` permanece arquivo órfã do manifest (intocada, regra 8); a parte que o contrato exigia
  (`situacao`) foi reaplicada idempotentemente pela migration corretiva `20261011120000`.
- `disciplinaId` do diário de classe é id fictício (não há tabela `sigov.disciplina` com FK); o
  fechamento não valida catálogo de disciplinas nesta fatia.
- Escola `ativo=false` não bloqueia vínculo novo (mesma política documentada na Sprint 2).
- Ano letivo é verificado por existência no tenant/entidade; vigência por data não é condição do comando.
