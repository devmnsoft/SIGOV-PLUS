# Relatório Ciclo 1 · Sprint 7 — Educação Avançada (biblioteca circulação: empréstimo + devolução + reserva por exemplar)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **circulação vertical** da biblioteca multi-esfera (regra 24) sobre as 10 tabelas `sigov.educacao_biblioteca_*` criadas na RC50.48: três comandos tipados novos que ligam acervo → exemplar → empréstimo → devolução/reserva, e um dashboard dedicado com métricas reais:

- **Comando tipado `POST /api/educacao/biblioteca/emprestimos/lancar`** com `{exemplarId, alunoId?, servidorId?, prazo}`:
  - Exemplar deve existir/ativo em `sigov.educacao_biblioteca_exemplar` → ausência **404 `BIBLIOTECA_EXEMPLAR_INEXISTENTE`**; fora do escopo institucional do contexto → **409 `EXEMPLAR_FORA_DO_ESCOPO`**; status não circulável (só `DISPONIVEL`/`ATIVO`) → **409 `EXEMPLAR_NAO_DISPONIVEL`**.
  - **Tomador = exatamente um** de aluno/servidor (nenhum ou ambos → **400 `EMPRESTIMO_TOMADOR_INVALIDO`**). Aluno validado em `sigov.aluno` (tenant, ativo → **404 `BIBLIOTECA_ALUNO_INEXISTENTE`**, divergência de entidade → 409 `CONTEXTO_INSTITUCIONAL_DIVERGENTE`); servidor validado em `sigov.servidor` (tenant, ativo → **404 `BIBLIOTECA_SERVIDOR_INEXISTENTE`**) — válido para qualquer esfera/órgão (regra 24).
  - `prazo >= data de lançamento` → senão **400 `PRAZO_EMPRESTIMO_INVALIDO`**.
  - **Um empréstimo vigente por exemplar × tenant**: repetição → **409 `EXEMPLAR_INDISPONIVEL`**, com índice único parcial como backstop transacional.
  - Persistência: insert em `educacao_biblioteca_emprestimo` (`ATIVA`, `competencia = hoje`, `data_inicio = hoje`, `data_fim = prazo`, `tipo='FISICO'`, auditoria `LACAR_EMPRESTIMO`). A rota usa sub-path `/lancar` porque a família avançada já expõe POST genérico em `/emprestimos` (conflito ASP0023 sob `-warnaserror`).
- **Comando tipado `POST /api/educacao/biblioteca/emprestimos/{id}/devolver`** com `{observacao?}`:
  - Emprestimo inexistente/inativo → **404 `BIBLIOTECA_EMPRESTIMO_INEXISTENTE`**; já encerrado (qualquer status não ativo) → **409 `EMPRESTIMO_JA_DEVOLVIDO`**.
  - **Transação única**: empréstimo → `ENCERRADA` (`justificativa = observação` ou `DEVOLUIDO`, auditoria append `DEVOLVER_EMPRESTIMO`) + insert em `educacao_biblioteca_devolucao` (`ATIVA`, `referencia_id = emprestimoId`, `competencia = hoje`, `dados = {em_dia, atraso_dias}` calculados do `data_fim`).
- **Comando tipado `POST /api/educacao/biblioteca/reservas/lancar`** com `{exemplarId, alunoId?, servidorId?}`:
  - Mesmas validações de exemplar e tomador; **uma reserva vigente por exemplar × tenant** (fila de tamanho 1 — simplificação declarada) → repetição **409 `RESERVA_VIGENTE_EXISTENTE`** com backstop de índice.
- **Dashboard dedicado `GET /api/educacao/biblioteca/dashboard`**: migrou do genérico para o DTO órfão pré-existente `EducacaoBibliotecaDashboardDto` com métricas reais do tenant — `titulos` (acervo ativo), `disponiveis` (exemplares ativos sem empréstimo vigente), `emprestimosAtivos` (não terminais) e `atrasos` (ativos com `data_fim < current_date`).

Sem novos estados na máquina de estados (S2): exemplar permanece `DISPONIVEL`; empréstimo/reserva entram `ATIVA` e saem pela família terminal já existente (`ENCERRADA`/`CANCELADA`), e terminal libera a vigência.

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011160000_evo_c1s7_biblioteca_circulacao.sql` | novo: índices únicos parciais `ux_biblioteca_emprestimo_tenant_exemplar_vigencia` e `ux_biblioteca_reserva_tenant_exemplar_vigencia` sobre `(tenant_id, coalesce(exemplar_id,0))` com `is_deleted=false and ativo=true and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` + pós-condição explícita (falha nomeada `S7-POSTCOND` se faltar índice) |
| `database/postgres/migrations/manifest.json` | entrada `20261011160000` registrada (sha256 `77a65efa55c1cc68128232df83590e14bcfc98ee5103323cc93a2748a10387f3`); 225 entradas |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1` (+`-Verify`) → S7 presente nos 6 consolidados (221 incluídas/4 excluídas) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | record novo `EducacaoBibliotecaCriarReservaRequest` + 4 métodos em `IEducacaoBibliotecaService` (`CriarEmprestimoAsync`, `DevolverEmprestimoAsync`, `CriarReservaAsync`, `BibliotecaDashboardAsync`) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | 11 falhas nomeadas novas: `BIBLIOTECA_EXEMPLAR_INEXISTENTE` (404), `BIBLIOTECA_EMPRESTIMO_INEXISTENTE` (404), `BIBLIOTECA_ALUNO_INEXISTENTE` (404), `BIBLIOTECA_SERVIDOR_INEXISTENTE` (404), `EXEMPLAR_FORA_DO_ESCOPO` (409), `EXEMPLAR_NAO_DISPONIVEL` (409), `EXEMPLAR_INDISPONIVEL` (409), `EMPRESTIMO_JA_DEVOLVIDO` (409), `RESERVA_VIGENTE_EXISTENTE` (409), `EMPRESTIMO_TOMADOR_INVALIDO` (400), `PRAZO_EMPRESTIMO_INVALIDO` (400) |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | `CriarEmprestimoAsync` (validações → transação com checagem de vigência + backstop 23505), `DevolverEmprestimoAsync` (transação: encerra empréstimo + registra devolução com `em_dia`/`atraso_dias`), `CriarReservaAsync` (validações + fila 1) e `BibliotecaDashboardAsync` (métricas dedicadas); helpers privados `ValidarTomador`/`CarregarExemplarNoEscopoAsync`/`ValidarTomadorExistenteAsync`; DTOs privados de leitura com aliases SQL explícitos (padrão Dapper das Sprints 4/6) |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | mapeamento HTTP das 11 novas exceções (+`CONTEXTO_INSTITUCIONAL_DIVERGENTE` no caminho de criação) nos catches existentes |
| `src/Sigov.Api/Controllers/EducacaoBibliotecaController.cs` | rotas tipadas `POST …/emprestimos/lancar`, `POST …/emprestimos/{id}/devolver`, `POST …/reservas/lancar`; `GET dashboard` agora retorna o DTO dedicado |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `BibliotecaCirculacao_Sprint7_EmprestimoDevolucaoReserva_UnicidadeVigenciaExemplar` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado para 226 (registro da migration S7) (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | reescrita do check E1 `dashboard.conjunto-completo` para o contrato S7 (DTO dedicado: `titulos=120` sobre o fixture, demais métricas 0, campos legados ausentes); bloco E11 (15 checks: fixture, acervo+exemplares via API, empréstimo com tomador, duplicado, backstop de índice, prazo anterior, sem tomador, exemplar inexistente, devolução transacional, devolução repetida, devolução inexistente, nova vigência após devolução, reserva+duplicada, dashboard dedicado com atraso, reserva cancelada reabrindo fila); CX ampliado (devoluções, empréstimos, reservas, exemplares C1S7-%, acervo C1S7-%, aluno C1S7-ALA, pessoa 999999000301, escola c1e26-s7-esc); scan case-insensitive das variáveis novas `$e11*` sem colisões |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e**.
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`S7-POSTCOND|t`); manifest + 6 consolidados sincronizados (regras 6–8).
- Suítes (execução limpa após build): Unit **521/521** · Integration **164/164** · Api **158/158**
  (0 ignorados; total 843; o delta de +1 na Integration é o fato estático novo da Sprint 7).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **89/89 PASS** (ts=2026-10-11T18:30:42Z),
  fixtures fictícios `C1S7-%`/`c1e26-s7-esc` (tenant 910001) aplicados e removidos ao final
  (resíduo zero); os 74 checks das Sprints 1–6 (E0–E10) revalidados na mesma rodada, exceto o check E1 `dashboard.conjunto-completo`, reescrito por consequência direta do escopo S7 (o endpoint migrou para o DTO dedicado).

## Evidência Tier-2 ao vivo (bloco E11 — circulação de biblioteca)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E11.0 | Fixture S7 (escola `c1e26-s7-esc` + pessoa/aluno fictício `C1S7-ALA`, tenant 910001) | exit=0 |
| E11.1 | Cadastro de acervo + 2 exemplares `DISPONIVEL` via API genérica | 200/200/200; ids dos exemplares 9/10 |
| E11.2 | **Emprestimo lançado** (exemplar A + aluno + prazo +14d) | 200; banco `ATIVA\|2026-10-11\|2026-10-25\|77` |
| E11.3 | Segundo empréstimo do mesmo exemplar vigente | **409 `EXEMPLAR_INDISPONIVEL`** nomeada |
| E11.4 | Insert SQL bruto de 2º empréstimo ATIVO do mesmo exemplar | exit=3 citando `ux_biblioteca_emprestimo_tenant_exemplar_vigencia` (backstop transacional) |
| E11.5 | Prazo anterior à data de lançamento | **400 `PRAZO_EMPRESTIMO_INVALIDO`** |
| E11.6 | Sem tomador (nem aluno nem servidor) | **400 `EMPRESTIMO_TOMADOR_INVALIDO`** |
| E11.7 | Exemplar inexistente (9999999999) | **404 `BIBLIOTECA_EXEMPLAR_INEXISTENTE`** |
| E11.8 | **Devolução registrada** (mesma transação) | 200; banco `ENCERRADA\|1\|true` (1 devolução, em_dia=true) |
| E11.9 | Devolução repetida | **409 `EMPRESTIMO_JA_DEVOLVIDO`** |
| E11.10 | Devolução de empréstimo inexistente | **404 `BIBLIOTECA_EMPRESTIMO_INEXISTENTE`** |
| E11.11 | Novo empréstimo após devolução | 200 (estado terminal liberou a vigência) |
| E11.12 | Reserva lançada + segunda reserva no mesmo exemplar | 200 (reserva id=4) + 409 `RESERVA_VIGENTE_EXISTENTE` (fila de tamanho 1) |
| E11.13 | **Dashboard dedicado** com fixture de atraso (`data_fim=ontem`) | 200; titulos/disponiveis/ativos/atrasos = 121/1/1/1 |
| E11.14 | Reserva cancelada via máquina de estados + nova reserva | 200/200 (terminal libera a fila) |
| CX | Limpeza do fixture (ampliada com devoluções, empréstimos, reservas, exemplares/acervo C1S7-%, aluno, pessoa e escola S7) | `restantes=0` |

(Blocos E0–E10 das Sprints 1–6 revalidados na mesma rodada: dashboard conjunto (check E1 reescrito para o DTO dedicado S7), CSV integral,
diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de versão, escopo
institucional, unicidade de matrícula, transferência com linhagem, correção auditada de frequência,
fechamento/reabertura de diário, publicação de histórico escolar versionado, cadastros base e operação
de merenda — todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

- **Coluna `status` inexistente em `sigov.aluno`**: a validação do tomador-aluno lia
  `select status … from sigov.aluno`, mas a tabela de aluno usa **`situacao`** (não o slot
  `status` das tabelas largas rc50_48). Primeira corrida ao vivo: 500 em todo empréstimo/reserva
  com tomador-aluno válido (`SqlState 42703 column "status" does not exist`, 6× no log da Api),
  enquanto os caminhos que falham antes da validação (400 prazo/tomador, 404 exemplar) passaram —
  sinal clássico de que a falha estava depois da checagem pré-transação. Corrigido removendo a
  leitura da coluna de estado e usando apenas `coalesce(entidade_id,-1)` com `long?` (null = aluno
  inexistente/inativo → 404 nomeada; `>=0` divergente do contexto → 409).
- **`DateOnly.today`/`DateOnly.Today` não existem neste SDK**: o primeiro build apontou CS0117;
  alinhado ao padrão do codebase (`DateOnly.FromDateTime(DateTime.Today)`, 37 usos existentes).
- **Concatenação de argumento sem parênteses no driver E2E (PowerShell)**: cinco chamadas do
  bloco E11 usavam `Get-Psql "…exemplar_id=" + $var + " …"`. Em modo argumento, o PowerShell
  interpreta o `+` fora das aspas como separador de argumentos — chegava ao psql apenas o prefixo
  (sem o id), produzindo `syntax error at end of input` em cascata (e um `update` de atraso que
  não casava linha nenhuma, zerando a métrica do dashboard). Detectado por repro com dump dos
  bytes do arquivo SQL escrito (`…exemplar_id=` + CRLF, sem o restante). Corrigido envolvendo os
  cinco argumentos em parênteses; auditoria por grep confirmou que todas as demais concatenações
  do bloco já estavam em parênteses ou dentro de interpolação `$()` (única string).

## Resíduos declarados (regra 13)

- Fila de reserva com tamanho 1 por exemplar (simplificação declarada; ordenação de fila e
  atribuição automática na devolução ficam para fatia futura).
- `servidorId` validado em `sigov.servidor` (tenant/ativo) mas sem checagem de entidade — master data
  de servidor é cross-esfera (entidades distintas podem compartilhar servidor); aluna/servidor têm
  tratamento assimétrico documentado.
- `validate-script-completop` falha em seed pré-existente `admin@sigov.local` (já em HEAD; não
  introduzido nesta sprint) — declarado, não mascarado.
- `rc50_25` permanece arquivo órfã do manifest (intocada, regra 8).
- RAM local cronicamente baixa (~1 GB livre) durante as corridas — ambiente de dev local, sem impacto
  no artifact; apps reiniciados com kill dirigido imediatamente antes da run validada.
