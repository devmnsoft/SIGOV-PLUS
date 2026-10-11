# Última execução

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 8 (transporte escolar: rota + ponto + vínculo de aluno)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 109/109 no PG16)**.

- Banco: migration idempotente `20261011170000_evo_c1s8_transporte_ponto_ordem.sql` aplicada 2× em `p1_dev` (pós‑condição `S8-POSTCOND|t`): índice único parcial `ux_transporte_ponto_tenant_rota_ordem` sobre `(tenant_id, coalesce(rota_id,0), coalesce(numero,0))` com `is_deleted=false and ativo=true and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` — uma ordem vigente por ponto × rota × tenant; reaproveitado como backstop o índice pré‑existente do rc50.48 `ux_educacao_transporte_aluno_rota_turno` (aluno × rota × turno); estados terminais liberam; falha nomeada se faltar índice (regra 13). Manifest 226 entradas + 6 consolidados regenerados (regra 7).
- C#: três comandos tipados novos em `IEducacaoTransporteEscolarService` + dashboard dedicado. `POST /api/educacao/transporte/rotas/lancar` {nome,turno,escolaId?,regiao?,codigo?}: exatamente uma de escolaId/regiao (senão 400 `ROTA_ESCOLA_OU_REGIAO_INVALIDA`; a região persiste em `dados->>'regiao'` — multi‑esfera regra 24: órgão/superintendência/região sem unidade escolar); escopo institucional reutilizado do S2 (404 `ESCOLA_INEXISTENTE`/409 `ESCOLA_FORA_DO_ESCOPO`); rota criada `ATIVA` (`tipo='ESCOLAR'`, turno normalizado — simplificação declarada). `POST …/rotas/{id}/pontos/lancar` {nome,ordem,descricao?}: rota inexistente → 404 `TRANSPORTE_ROTA_INEXISTENTE`; não ativa → 409 `TRANSPORTE_ROTA_NAO_VIGENTE`; ordem>0 (senão 400 `PONTO_ORDEM_INVALIDA`); uma ordem vigente por rota×tenant → 409 `PONTO_ORDENACAO_EXISTENTE` + backstop de índice; ponto criado na mesma transação herdando turno/entidade/escola da rota. `POST …/rotas/{id}/alunos/vincular` {alunoId,pontoId?,turno?}: aluno validado em `sigov.aluno` (404 `TRANSPORTE_ALUNO_INEXISTENTE`; divergência de entidade → 409); turno herdado da rota (divergente → 400 `TURNO_VINCULO_DIVERGENTE`); ponto inexistente → 404 `TRANSPORTE_PONTO_INEXISTENTE`, de outra rota → 409 `PONTO_ROTA_DIVERGENTE` (gravado em `referencia_id`); um vínculo vigente por aluno×rota×turno → 409 `ALUNO_JA_VINCULADO_A_ROTA` + backstop rc50.48. Dashboard `GET /api/educacao/transporte/dashboard` migrou do genérico para o DTO pré‑existente `EducacaoTransporteDashboardDto` (rotasAtivas/pontosAtivos/alunosVinculados/escolasAtendidas sobre dados reais do tenant). Sem novos estados na máquina S2: terminal bloqueia nova operação e libera ordem/vínculo.
- Defeitos corrigidos na prova ao vivo (sem máscara): nenhum de código — o bloco E12 (20 checks) passou integralmente na primeira corrida oficial válida e os 89 checks das Sprints 1–7 revalidaram na mesma rodada (109/109); duas corridas anteriores foram descartadas por falha de ambiente (apps caídos por RAM esgotada, `env.web-up`/`env.api-up`=000), corrigida operacionalmente (ver nota de ambiente).
- Gates: build Release `-warnaserror` de `sigov.sln` **0w/0e**; suítes limpas Unit **521/521** · Integration **165/165** (fato estático novo `TransporteEscolar_Sprint8_RotaPontoAluno_UnicidadeOrdemEVinculo` + count do manifest atualizado para 227, nas classes existentes, regra 14) · Api **158/158** (0 ignorados; total 844); Tier‑2 E2E HTTP ao vivo **109/109 PASS** (ts=2026-10-11T20:24:04Z, driver `scripts/c1_tier2_e2e.ps1`, bloco E12 com 20 checks: fixture, **rota lançada** com escola+turno, sem escola nem região 400, escola+região juntos 400, **regional sem escola (multi‑esfera)** 200, escola inexistente 404, **ponto com ordem** 200 (turno herdado), ordem duplicada 409, backstop de índice (insert bruto → exit=3 citando `ux_transporte_ponto_tenant_rota_ordem`), ordem inválida 400, rota inexistente 404, **vínculo aluno→rota** 200, vínculo duplicado 409, backstop aluno×rota×turno (exit=3 citando `ux_educacao_transporte_aluno_rota_turno`), turno divergente 400, aluno inexistente 404, ponto de outra rota 409, vínculo com ponto próprio 200, **dashboard dedicado** (rotasAtivas/pontosAtivos/alunosVinculados/escolasAtendidas = 2/2/2/1), rota cancelada bloqueia operações 200/409 — todos PASS; fixtures removidos, resíduo zero). E0–E11 (Sprints 1–7) revalidados na mesma rodada.
- Nota de ambiente (regra 13, não mascarada): memória livre cronicamente baixa neste host (≈1 GB durante as corridas); apps reiniciados com kill dirigido imediatamente antes das corridas. Resíduos declarados: rotas entram direto em `ATIVA` no comando tipado (`RASCUNHO→ATIVA` segue via máquina nas rotas genéricas); unicidade do ponto é por **ordem** por rota×tenant (nome não é único); vínculo guarda o ponto em `referencia_id` (slot genérico das tabelas largas); `validate-script-completop` (seed `admin@sigov.local` já em HEAD) e `rc50_25` órfã permanecem como declarado nas sprints anteriores.
- Próximo exato: **Ciclo 1 — próxima fatia da trilha RCEVO** (ciclos restantes conforme `BACKLOG_EXECUTAVEL.md`). GED por último.

---

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 7 (biblioteca circulação: empréstimo + devolução + reserva por exemplar)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 89/89 no PG16)**.

- Banco: migration idempotente `20261011160000_evo_c1s7_biblioteca_circulacao.sql` aplicada 2× em `p1_dev` (pós‑condição `S7-POSTCOND|t`): índices únicos parciais `ux_biblioteca_emprestimo_tenant_exemplar_vigencia` e `ux_biblioteca_reserva_tenant_exemplar_vigencia` sobre `(tenant_id, coalesce(exemplar_id,0))` com `is_deleted=false and ativo=true and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` — um empréstimo vigente e uma reserva vigente (fila de tamanho 1) por exemplar × tenant; estados terminais liberam a vigência; falha nomeada se faltar índice (regra 13). Manifest 225 entradas + 6 consolidados regenerados (regra 7).
- C#: três comandos tipados novos em `IEducacaoBibliotecaService` + dashboard dedicado. `POST /api/educacao/biblioteca/emprestimos/lancar` {exemplarId,alunoId?,servidorId?,prazo}: exemplar deve existir/ativo (404 `BIBLIOTECA_EXEMPLAR_INEXISTENTE`; fora do escopo institucional → 409 `EXEMPLAR_FORA_DO_ESCOPO`; status não circulável → 409 `EXEMPLAR_NAO_DISPONIVEL`); tomador = exatamente um de aluno/servidor (senão 400 `EMPRESTIMO_TOMADOR_INVALIDO`; aluno validado em `sigov.aluno` → 404 `BIBLIOTECA_ALUNO_INEXISTENTE`, divergência de entidade → 409; servidor em `sigov.servidor` → 404 `BIBLIOTECA_SERVIDOR_INEXISTENTE` — válido para qualquer esfera, regra 24); `prazo >= hoje` (400 `PRAZO_EMPRESTIMO_INVALIDO`); transação única: checagem de vigência (409 `EXEMPLAR_INDISPONIVEL`, backstop índice 23505) + insert `ATIVA` (`competencia = data_inicio = hoje`, `data_fim = prazo`, auditoria `LACAR_EMPRESTIMO`). `POST /api/educacao/biblioteca/emprestimos/{id}/devolver` {observacao?}: inexistente/inativo → 404 `BIBLIOTECA_EMPRESTIMO_INEXISTENTE`; não vigente → 409 `EMPRESTIMO_JA_DEVOLVIDO`; transação única encerra o empréstimo em `ENCERRADA` (justificativa = observação ou `DEVOLUIDO`, auditoria append `DEVOLVER_EMPRESTIMO`) + insert da devolução em `educacao_biblioteca_devolucao` (`ATIVA`, `referencia_id = emprestimoId`, `em_dia`/`atraso_dias` calculados do `data_fim`). `POST /api/educacao/biblioteca/reservas/lancar` {exemplarId,alunoId?,servidorId?}: mesmas validações; uma reserva vigente por exemplar × tenant → 409 `RESERVA_VIGENTE_EXISTENTE` + backstop de índice. Dashboard `GET /api/educacao/biblioteca/dashboard` migrou do genérico para o DTO pré‑existente `EducacaoBibliotecaDashboardDto` (titulos/disponiveis/emprestimosAtivos/atrasos sobre dados reais do tenant). Rotas usam sub‑paths `/lancar` e `/{id}/devolver` (conflito ASP0023 com o POST genérico da família). Sem novos estados na máquina S2: exemplar permanece `DISPONIVEL`; empréstimo/reserva entram `ATIVA` e saem pela família terminal já existente.
- Defeitos corrigidos na prova ao vivo (sem máscara): (a) a validação do tomador‑aluno lia a coluna `status` inexistente em `sigov.aluno` (a tabela usa `situacao`; as largas rc50_48 usam `status`) → 500 em todo empréstimo/reserva com aluno válido (`SqlState 42703`, 6× no log da Api), enquanto os caminhos que falham antes (400 prazo/tomador, 404 exemplar) passavam — falha corrigida lendo apenas `coalesce(entidade_id,-1)` como `long?`; (b) driver E2E: cinco chamadas do bloco E11 usavam `Get-Psql "…exemplar_id=" + $var + " …"` **sem parênteses** — em modo argumento o PowerShell trata o `+` como separador e só o prefixo chegava ao psql (`syntax error at end of input`; um `update` de atraso casava zero linhas); detectado com dump dos bytes do arquivo SQL gerado; corrigido envolvendo os cinco argumentos; (c) `DateOnly.today` não existe neste SDK (CS0117) → padrão do codebase `DateOnly.FromDateTime(DateTime.Today)`.
- Gates: build Release `-warnaserror` de `sigov.sln` **0w/0e**; suítes limpas Unit **521/521** · Integration **164/164** (fato estático novo `BibliotecaCirculacao_Sprint7_EmprestimoDevolucaoReserva_UnicidadeVigenciaExemplar` + count do manifest atualizado para 226, nas classes existentes, regra 14) · Api **158/158** (0 ignorados; total 843); Tier‑2 E2E HTTP ao vivo **89/89 PASS** (ts=2026-10-11T18:30:42Z, driver `scripts/c1_tier2_e2e.ps1`, bloco E11 com 15 checks: fixture, acervo+exemplares via API (ids 9/10), **empréstimo lançado** (`ATIVA|2026-10-11|2026-10-25|aluno 77`), duplicado 409, backstop de índice (insert bruto → exit=3 citando `ux_biblioteca_emprestimo_tenant_exemplar_vigencia`), prazo anterior 400, sem tomador 400, exemplar inexistente 404, **devolução transacional** (banco `ENCERRADA|1 devolução|em_dia=true`), devolução repetida 409, devolução inexistente 404, nova vigência após devolução 200, reserva+duplicada 409 `RESERVA_VIGENTE_EXISTENTE`, **dashboard dedicado** (121/1/1/1 com fixture de atraso `data_fim=ontem`), reserva cancelada reabre fila 200/200 — todos PASS; fixtures removidos, resíduo zero). E0–E10 (Sprints 1–6) revalidados na mesma rodada; o check E1 `dashboard.conjunto-completo` foi reescrito por consequência direta do escopo S7 (endpoint migrado para o DTO dedicado).
- Nota de ambiente (regra 13, não mascarada): memória livre cronicamente baixa neste host (≈1 GB durante as corridas); apps reiniciados com kill dirigido imediatamente antes das corridas (política desde a Sprint 6). Resíduos declarados: fila de reserva com tamanho 1 por exemplar (ordenação de fila e atribuição automática na devolução ficam para fatia futura); `servidorId` validado sem checagem de entidade (master data cross‑esfera — assimetria documentada em relação ao aluno); `validate-script-completop` (seed `admin@sigov.local` já em HEAD) e `rc50_25` órfã permanecem como declarado nas sprints anteriores.
- Próximo exato: **Ciclo 1 Sprint 8 — transporte escolar (rota/ponto/aluno)**. GED por último.

---

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 6 (merenda escolar: operação — estoque + distribuição de refeição com consumo transacional)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 74/74 no PG16)**.

- Banco: migration idempotente `20261011150000_evo_c1s6_merenda_distribuicao_consumo_estoque.sql` aplicada 2× em `p1_dev` (pós‑condição `S6-POSTCOND|true`): índice único parcial `ux_merenda_distribuicao_tenant_cardapio_data` sobre `(tenant_id, referencia_id, competencia)` com `is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` — uma distribuição vigente por cardápio × data; estados terminais liberam o dia; falha nomeada se faltar o índice (regra 13). Manifest 224 entradas + 6 consolidados regenerados (regra 7).
- C#: dois comandos tipados novos em `IEducacaoMerendaService`. `POST /api/educacao/merenda/estoques` {produtoId,saldoInicial}: produto deve existir/estar ativo (senão 404 `MERENDA_PRODUTO_INEXISTENTE`); sem linha → cria espelhando `nome`/`unidade` do produto abrindo com o saldo informado; com linha → soma ajuste (sem duplicar). `POST /api/educacao/merenda/distribuicoes/confirmar` {cardapioId,data,itens[{produtoId,quantidade}]}: validações pré-transação (itens válidos/sem repetição, produto restrito ao menu → 400 `PRODUTO_FORA_DO_MENU`; cardápio existe → 404 `DISTRIBUICAO_CARDAPIO_INEXISTENTE`, no escopo institucional → 409 `CARDAPIO_FORA_DO_ESCOPO`, status ATIVO/ATIVO‑vigente → 409 `CARDAPIO_NAO_APROVADO`; data no período → 409 `DATA_FORA_DO_PERIODO`), e transação única com locks `FOR UPDATE` ordenados pelas linhas de estoque: checagem do dia ocupado (409 `DISTRIBUICAO_DIA_EXISTENTE`, backstop = índice) → estoque suficiente por item (409 `ESTOQUE_INSUFICIENTE`) → insert da distribuição (`ATIVA`, `competencia=data`) → itens espelhados → movimentos `SAIDA` (−quantidade) → baixa de saldo. Rota usa sub‑path `/confirmar` (conflito ASP0023 com o POST genérico da família em `/distribuicoes`).
- Defeitos corrigidos na prova ao vivo (sem máscara): (a) materialização Dapper em record positional exigia construtor casando **exatamente** os nomes das colunas (sem remover underscore) → 500 genérico em toda chamada de `distribuicoes/confirmar`; corrigido com classe privada + aliases SQL explícitos (`id as Id, entidade_id as EntidadeId, …`), padrão da Sprint 4; (b) driver E2E: `$pA/$pB/$pC` (resposta HTTP) e `$pa/$pb/$pc` (ids psql) são a **mesma** variável no PowerShell (case‑insensitive) — as leituras psql sobrescreviam as respostas HTTP e o check lia o exit code do psql (0); renomeadas para `$pApi*` + `--max-time 45` + captura de stderr do curl no `Invoke-Api`; (c) evidência SQL normalizada com `round(…)::text` (numeric com escala imprimia `100.0000`).
- Gates: build Release `-warnaserror` de `sigov.sln` **0w/0e**; suítes limpas Unit **521/521** · Integration **163/163** (fato estático novo `MerendaOperacao_Sprint6_DistribuicaoComConsumoEstoque_UnicidadeDia` na classe existente, regra 14) · Api **158/158** (0 ignorados); Tier‑2 E2E HTTP ao vivo **74/74 PASS** (ts=2026-10-11T16:51:48Z, driver `scripts/c1_tier2_e2e.ps1`, bloco E10 com 12 checks: fixture, produtos via API (ids 33/34/35), estoque inicial (`100|Arroz C1S6`), ajuste sobre linha existente (10+5→15), 404 produto inexistente, cardápio ATIVA, **distribuição confirmada com consumo real** (`ATIVA|2026-11-10|2 itens|2 movimentos SAIDA|saldo total 61`), 409 estoque insuficiente, 409 dia ocupado, 409 data fora do período, 400 produto fora do menu, 409 cardápio rascunho — todos PASS; fixtures removidos, resíduo zero). E0–E9 (Sprints 1–5) revalidados na mesma rodada.
- Nota de ambiente (regra 13, não mascarada): memória livre cronicamente baixa neste host (≈0,4–4 GB durante as corridas); a instabilidade dos apps locais era causada pelo kill genérico `Stop-Process dotnet` do script de restart matando os nodes de build do C# Dev Kit (VS Code) e disparando respawn storm de MSBuild — corrigido com kill dirigido apenas aos processos SIGOV/listeners das portas. Resíduos declarados: requisição de compra derivada da distribuição e unidades de consumo por aluno/turma ficam para as próximas fatias; `validate-script-completop` e `rc50_25` órfã permanecem como declarado nas sprints anteriores.
- Próximo exato: **Ciclo 1 Sprint 7 — complementares A/B (biblioteca)**. GED por último.

---

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 5 (merenda escolar: cadastro base de cardápio com produtos + unicidade de período)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 62/62 no PG16)**.

- Banco: migration idempotente `20261011140000_evo_c1s5_merenda_cardapio_unicidade_periodo.sql` aplicada 2× em `p1_dev` (pós‑condição `true`): índice único parcial `ux_merenda_cardapio_tenant_escola_inicio` sobre `(tenant_id, escola_id, data_inicio)` com `is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` — um cardápio vigente por escola × data de início; estados terminais liberam o período (mesma política da unicidade de matrícula da Sprint 3); falha nomeada `S5-POSTCOND` se faltar o índice (regra 13). Manifest 223 entradas + 6 consolidados regenerados (regra 7).
- C#: o comando tipado que existia só como contrato (`EducacaoMerendaCriarCardapioRequest`/`IEducacaoMerendaCardapioService`) ficou implementado: `POST /api/educacao/merenda/cardapios/completo` {escolaId,inicio,fim,produtos} com escopo institucional da escola (reaproveita Sprint 2 #6), produtos válidos/ativos do tenant, sem duplicidade de produto no pedido, checagem de período ocupado antes do insert, e persistência transacional do cardápio (`RASCUNHO`) + itens `educacao_merenda_cardapio_item` espelhando `nome`/`unidade` do produto; falhas nomeadas novas: `CARDAPIO_PERIODO_EXISTENTE` (409), `CARDAPIO_PRODUTO_DUPLICADO` (409), `CARDAPIO_PRODUTO_INEXISTENTE` (404) + 400 de período inválido + `ESCOLA_FORA_DO_ESCOPO` (409). Máquina de estados S2 reaproveitada no cardápio (`RASCUNHO→ATIVA→ENCERRADA`, terminal não reabre).
- Gates: build Release `-warnaserror` de `sigov.sln` **0w/0e**; suítes limpas Unit **521/521** · Integration **162/162** (fato estático novo na classe existente, regra 14) · Api **158/158** (0 ignorados); Tier‑2 E2E HTTP ao vivo **62/62 PASS** (ts=2026-10-11T14:31:31Z, driver `scripts/c1_tier2_e2e.ps1`, bloco E9 com 10 checks: fixture, produtos via API, cardápio+itens transacionais, backstop de índice (insert bruto → exit=3 unique violation), duplicidades, inexistente, período inválido, escopo, máquina terminal — todos PASS na primeira corrida; fixtures removidos, resíduo zero). E0‑E8 (Sprints 1‑4) revalidados na mesma rodada.
- Nota de ambiente (regra 13, não mascarada): memória livre cronicamente baixa neste host (≈0,58 GB de 15,7 GB antes do gate); apps relançados imediatamente antes da corrida E2E (mesma política das Sprints 2–4). Resíduos declarados: operação de merenda (distribuição/consumo/requisição ligando cardápio a estoque e compras) fica para a Sprint 6; quantidade por item de cardápio ainda não faz parte do comando base; `validate-script-completop` e `rc50_25` órfã permanecem como declarado nas sprints anteriores.
- Próximo exato: **Ciclo 1 Sprint 6 — merenda escolar (operação)**. GED por último.

---

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 4 (notas e boletim: publicação de histórico escolar versionado + portais)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 52/52 no PG16)**.

- Banco: migration idempotente `20261011130000_evo_c1s4_educacao_historico_escolar_publicar.sql` aplicada 2× em `p1_dev` (pós‑condição `true|true`; permissão id=1848; 1 concessão p/ tenant 910001, 18 no total): permissão granular `educacao.historico_escolar.publicar` (módulo educacao/recurso historico_escolar/ação publicar) + concessão `PERMITIR` aos perfis administrativos por tenant (padrão RC50.29) + falha nomeada se faltar permissão ou concessão (regra 13). Manifest 222 entradas + 6 consolidados regenerados (regra 7).
- C#: avaliação do diário ganha `componenteCurricular` + notas por aluno (`Alunos` jsonb em `educacao_diario_avaliacao.dados`, com validação nomeada: valor máximo positivo, nota em `[0,máx]`, aluno válido); novo comando `POST /api/educacao/portal/admin/historico-escolar/{alunoId}/publicar` (guard `portal-boletim` + administrativo + `Pode("educacao.historico_escolar.publicar")`) com agregação transacional por componente curricular (médias das avaliações `dados->Alunos` FULL JOIN frequência `100*presenças/total`, turmas do aluno no ano letivo da matrícula‑âncora), histórico versionado (`dados->versao` incrementado, status `PUBLICADO`, auditoria) e itens substituídos; falhas nomeadas: matrícula alheia/sem turma → 404, escopo institucional divergente → 400, sem lançamentos → 400 (sem sucesso vazio simulado).
- Defeitos corrigidos na prova ao vivo: boletim/vida‑escolar do portal tinham latência de 500 (armadilha Dapper/timestamptz em `DateTimeOffset` — `BoletimHeader.AtualizadoEm` e `VidaEscolarDto.AtualizadoEm` alinhados à convenção `DateTime` dos DTOs Bloco3; só apareceu porque nunca haviam sido exercidos com dados publicados); driver E8 ajustado às datas dentro da janela canônica (`data_aula ≥ data_matricula` e ≤ fim do ano letivo — comportamento do banco mantido).
- Gates: build Release `-warnaserror` de `sigov.sln` **0w/0e**; suítes limpas Unit **521/521** · Integration **161/161** (fato estático novo na classe existente, regra 14) · Api **158/158** (0 ignorados); Tier‑2 E2E HTTP ao vivo **52/52 PASS** (ts=2026-10-11T14:03:08Z, driver `scripts/c1_tier2_e2e.ps1`, fixtures fictícios removidos, resíduo zero): vínculo de portal pela secretaria, avaliação com notas no jsonb, 3 chamadas (2 PRESENTE + 1 FALTA), **publicação v1 → item MATEMATICA 9.00/66.67%**, boletim e vida‑escolar do portal reagregados, **republicação → v2**, 404 matrícula alheia, 400 sem lançamentos, permissão/banco autoridade; E0‑E7 (Sprints 1‑3) revalidados na mesma rodada.
- Nota de ambiente (regra 13, não mascarada): memória livre cronicamente baixa neste host (≈0,6 GB de 15,7 GB antes do gate); apps relançados imediatamente antes da corrida E2E (mesma política das Sprints 2/3). Resíduos declarados: média ponderada por `peso`/reprovação por componente fica para o boletim definitivo (regras parametrizadas por esfera, regra 24); `disciplinaId` segue fictício; `validate-script-completop` e `rc50_25` órfã permanecem como declarado nas sprints anteriores.
- Próximo exato: **Ciclo 1 Sprint 5 — merenda escolar (cadastro base)**. GED por último.

---

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 3 (ciclo escolar: unicidade por oferta + correção auditada de frequência)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 41/41 no PG16)**.

- Banco: migration corretiva idempotente `20261011120000_evo_c1s3_educacao_matricula_unicidade_oferta.sql` aplicada 2× em `p1_dev` (post‑condição verdadeira): (a) índice único parcial `ux_matricula_tenant_aluno_turma_vigencia` (aluno×turma com status ATIVA/CONFIRMADA; `turma_id NULL` não conflita; duplicados pré-existentes falham explicitamente, regra 13); (b) `diario_frequencia.situacao` restaurada (órfa do `rc50_25`, nunca aplicada) com backfill a partir de `presente` + CHECK canônico; (c) remoção do índice estreito legado sem `componente_curricular`. Manifest 221 entradas + 6 consolidados sincronizados (regra 7).
- C#: `TransferirAsync` expõe o id da nova matrícula (`Result<long>`, rota responde `ApiResponse<long>`); correção auditada de frequência (`POST /api/educacao/frequencias/{id}/correcao`) com evento `FrequenciaCorrigida` (de/para/justificativa) sob lock `for update`; bloqueio nomeado quando o diário está `FECHADO` + reabertura com justificativa e histórico; guarda `frequencia/criar` corrigida (a inexistente `diario_frequencia/criar` causava 403 universal); Bloco3: pré‑serialização de listas POCO para o jsonb writer do Npgsql e `FechamentoDto.CreatedAt` em `DateTime` (materialização Dapper).
- Gates: build Release `-warnaserror` de `sigov.sln` **0w/0e**; suítes limpas Unit **521/521** · Integration **160/160** · Api **158/158** (0 ignorados); Tier‑2 E2E HTTP ao vivo **41/41 PASS** (ts=2026-10-11T13:07:53Z, driver `scripts/c1_tier2_e2e.ps1`, fixtures fictícios `c1e26-%`/`C1S3-%` removidos, resíduo zero). Race da última vaga: exatamente 1×200/1×400 (sem oversell); transferência: origem `TRANSFERIDA` + nova `ATIVA` com `origem=TRANSFERENCIA:<id>` e 2 eventos de auditoria; correção bloqueada sob `FECHADO` e OK pós‑reabertura.
- Defeitos corrigidos na prova ao vivo: permissão canônica de criação de frequência; id ausente na resposta de transferência; 500 do jsonb writer em lançamento de frequência do diário (lista POCO como parâmetro); 500 de materialização Dapper no histórico de fechamentos; artefatos do driver PS 5.1 (quoting de `Start‑Process` → `HttpClient.SendAsync`; CTE entre sentenças → temp table; stderr do psql vira dado; bodies HTTP em UTF‑8 + BOM no script; booleanos do PG14+ chegam como `true/false`).
- Nota de ambiente (regra 13, não mascarada): a máquina opera com memória livre cronicamente baixa (≈0,4 GB livres de 15,7 GB); duas execuções de apps locais morreram por pressão de memória durante corridas E2E intermédias — reexecução após relançar os apps confirmou estabilidade do fluxo (41/41).
- Próximo exato: **Ciclo 1 Sprint 4 — notas e boletim (publicação de histórico escolar agregando avaliações do diário + frequência) + portais**.

---

Data: 2026-10-11. Ciclo 1 Educação Avançada — **Sprint 2 (#4 máquinas de estado por recurso + #6 escopo institucional)**. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 25/25 no PG16)**.

- Base: trabalho retomado no diretório canônico `C:\MNSOFT\SIGOV-PLUS` (os commits do Ciclo 0/1 e o WIP da Sprint 2 estavam na worktree `swift-circuit`; promovidos via fast‑forward + commit do WIP). Banco `p1_dev` no container oficial `sigov-postgres` (PG **16.14**) via forwarder `p1fwd` porta 5433; apps locais Web :5099 + API :5100 (`MigrationMode=Disabled`).
- Entregues: (a) `EducacaoStatusMaquina` — transições por FAMÍLIA de status multi-recurso (regra 24), `TRANSICAO_INVALIDA` 409, no-op idempotente auditado ao próprio estado; (b) guarda de versão otimista `coalesce(updated_at,created_at)=@VersaoAtual` → `CONFLITO_VERSAO` 409 sem sobrescrita silenciosa, contrato expõe a versão efetiva em `updatedAt`; (c) `REGISTRO_NAO_ENCONTRADO`/`RECURSO_INDISPONIVEL` 404 nomeados; (d) **#6 escopo institucional**: escola/ano letivo validados contra tenant+entidade antes do insert (`ESCOLA_INEXISTENTE` 404, `ESCOLA_FORA_DO_ESCOPO` 409, `ANO_LETIVO_INEXISTENTE` 422) e comando de status sobre registro de outra entidade → `CONTEXTO_INSTITUCIONAL_DIVERGENTE` 409; endpoints de criação passam por `ExecutarCriarAsync` com mapeamento HTTP explícito.
- Defeitos runtime descobertos na prova ao vivo e corrigidos sem máscara: (1) Dapper `ExecuteScalarAsync` não materializa tupla multi-coluna (500 em toda transição) → `QueryFirstOrDefaultAsync<(Status,LinhaEntidade)>`; (2) `updatedAt` nulo em linhas recém-criadas quebrava a guarda no cliente → select passa a expor `coalesce(updated_at,created_at)`.
- Evidência Tier‑2 ao vivo — driver reproduzível `scripts/c1_tier2_e2e.ps1` (**25/25 PASS**, ts=2026-10-11T00:24Z): E1 dashboard conjunto completo (120/60/20/20+6 recentes); E2 CSV integral 122 linhas BOM+escape; E3 diagnóstico de schema ausente (probe rename); E4 auditoria append-only de/para/motivo; E5 máquina de estados completa (pendente não encerra 409, rascunho→ativa 200, terminal não reabre 409, registro inexistente 404 nomeado, **duplo envio com versão obsoleta → 409 CONFLITO_VERSAO**); E6 escopo (escola inexistente 404, ano ausente 422, mesma entidade aceita, outra entidade 409) com fixture dinâmico de entidades/escolas fictícias removido ao final (resíduo zero).
- Gates: build Release `-warnaserror` **0w/0e**; Unit **521/521**; Integration/Api reexecução com `DOTNET_HOST_FACTORY_RESOLVER_DEFAULT_TIMEOUT_IN_SECONDS=900` — resultados na seção de fechamento desta sprint (falhas anteriores eram ambientais: vstest/host timeout sob carga nesta máquina, não código).
- Nota de ambiente (regra 13/18, não mascarada): o boot dos hosts ASP.NET Core chega a levar vários minutos neste host (CPU ~0, sem rede — compatível com scan AV dos binários recém-compilados); o `dotnet run` pode gerar processos órfãos que disputam porta 5099/5100 — usar `dotnet exec <dll>` com log direto e confirmar owner único da porta antes do gate. Sem migration nesta sprint (schema existente; consolidados intocados).
- Próximo item exato: **Ciclo 1 Sprint 3 — ciclo escolar** (matrícula/enturmação com capacidade sob concorrência, transferências atômicas com histórico, correção de frequência auditada, fechamento/reabertura de período). GED por último.

---

Data: 2026-10-10. Ciclo 0 (audit do runner) + Ciclo 1 Educação Avançada — Sprint 1 das correções comprovadas. Estado: **VALIDADO AO VIVO (build/suítes + Tier‑2 E2E HTTP 12/12 no PG16)**.

- Base: worktree `swift-circuit`, HEAD `f6e4aa4c` (merge da ponta de `origin/main` `aaec9515` com a linha local P0/Gate B); SDK .NET 10; banco `p1_dev` no container oficial `sigov-postgres` (PG16) via forwarder socat porta 5433; apps locais Web :5099 + API :5100 (`MigrationMode=Disabled`).
- Ciclo 0 — audit do runner `database/docker/apply-migrations.sh`: reescrita RC‑EVO‑C §3.1 auditada linha a linha (dual lock: gate de fase `GATE_LOCK=x'5349474F564D4947'` + xact‑lock por entrada `MIGRATION_LOCK=x'5349474F56504C55'`; 1 txn/entrada; `statement_timeout=30min`; dual ledger RECONCILIADA/REPLICADA + pós-condição). Resíduos não mascarados (regra 13): **C0‑R1** — janitor (linha 49) mata gate por fingerprint+idade >45 min; execução legítima longa de clean‑install pode perder o gate de fase (mudança adiada para RC própria); **C0‑F1** — sonda `sigov.evo_c_probe_aplicada` é sentinela inerte fora do manifest (220 entradas / 230 `.sql`) e sem DROP em lugar nenhum (orfanato apenas em bancos legado EVO‑C; limpeza recomendada, não aplicada). Relatório: `docs/evidencias/rcevo/relatorio_ciclo0_migration_audit_apply_migrations.md`.
- Ciclo 1 Sprint 1 — correções comprovadas de Educação Avançada (escopo #1/#2/#3/#5/#7; **residuais declarados: #4** máquinas de estado por recurso e **#6** escopo institucional completo, para Sprint 2): (1) dashboard em `QueryMultipleAsync` único com `count(1) filter(...)` sobre o conjunto completo + 6 recentes; (2)/(3) exportação CSV integral via helper novo `EducacaoCsv.Campo` (guarda de fórmula com prefixo `'` para `=+-@\t\r`, aspas RFC, BOM, `InvariantCulture`); (5) `PagedResult<T>.Indisponivel(page,size,diagnóstico)` — schema ausente vira diagnóstico operacional explícito, não lista vazia (regra 13); (7) auditoria append‑only de transição de status com `de/para/motivo`.
- Defeitos runtime descobertos na prova ao vivo e corrigidos em código (sem máscara): (a) Dapper não materializa `IDictionary<string,object>` em `QueryMultiple` ("Invalid type owner for DynamicMethod") → leitura tipada `record EducacaoTotais`; (b) alias SQL não-aspirado chega minúsculo no reader (`createdat`) quebrando o construtor do DTO → `created_at as "CreatedAt"` aspirado nos três SELECTs; (c) expectativa do driver E4 assumia linha inicial RASCUNHO quando a linha 1 do fixture nasce ATIVA (erro do teste, corrigido no script).
- Evidência Tier‑2 ao vivo — driver reproduzível `scripts/c1_tier2_e2e.ps1` (auto-aplica fixture idempotente fictício `c1e26-%` do tenant 910001, 120 linhas, e remove ao final): **12/12 PASS** (ts=2026-10-10T12:23Z): login 302→MinhaCentral + bearer 64 chars; **dashboard `total=120 ativos=60 pendentes=20 alertas=20 recentes=6`** (código antigo veria 0/0/0); **CSV 122 linhas** (cabeçalho + 120 + 1 extra por quebra embutida em campo aspas) com **BOM UTF‑8**, célula de fórmula íntegra e newline preservado (fixo antigo truncava em 101); probe de rename transitório provou o **diagnóstico "schema ausente"** ao vivo (200/items=0) com restauração sem perda; transições ATIVA→ATIVA→CANCELADA gravaram **2 entradas `acao=STATUS` com `de/para/motivo` preservando a anterior** (append‑only); resíduo zero ao final. Primeira rodada 9/12, com as 3 falhas sendo exatamente os itens (a), (b) e (c); segunda rodada 12/12.
- Gates: build Release `-warnAsError` **0w/0e**; suítes executadas Unit **515/515**, Integration **159/159**, Api **158/158** (0 ignorados; apenas ampliação de classes existentes — regra 14). Sem migration nesta sprint (schema existente reutilizado; regras 6–8 intactas). Nota de ambiente (regra 13/18): host sem `psql` (SQL via `docker cp` + `psql -f/-c`); container `sigov-api` pré-existente em loop de reinício contornado pela API local.
- Estado final: `p1_dev` preservado (fixture removido), apps locais finalizados (ports 5099/5100 livres), forwarder mantido. Artefatos em `artifacts/c1_tier2_e2e/`; logs em `Temp\opencode\c1_{web,api}*2.log`. Evidência completa: `docs/evidencias/rcevo/relatorio_ciclo1_educacao_avancada_sprint1_tier2.md`. Próximo item exato: **Ciclo 1 Sprint 2 — #4 máquinas de estado por recurso + #6 escopo institucional**; em seguida os ciclos restantes RCEVO. GED por último.

---

Data: 2026-10-10. Gate B (P1) — RC-SAAS-AUT validado ao vivo no PG16 oficial. Estado: **PASS (26/26, evidência ao vivo)**.

- Host: worktree `swift-circuit`; prova de runtime em HEAD `c1fbf922` (base da RC-SAAS-AUT, commit `74f763`); consolidação dual-catalog em `f388b67d`. SDK .NET 10 (10.0.400, honrando `global.json` 10.0.100 / rollForward latestFeature). Banco `p1_dev` no container oficial `sigov-postgres` (PostgreSQL **16.14**); seeds homolog A/B/C/D + fixture idempotente `20261010_gateb_rcsaas_aut_fixtures.sql` (perfis `ADMIN_TENANT` 90–93 + probe `gate.nao.admin`).
- Nota de ambiente (regra 13/18, não mascarada): host sem `psql` (SQL via `docker cp` + `docker exec ... psql -f`); porta 5432 sombreada por Postgres nativo do Windows → PG16 oficial via forwarder `alpine/socat` (`p1fwd`) na porta livre 5433 → `sigov-postgres:5432`; container `sigov-api` pré-existente em loop de reinício (estado ambiente) contornado executando Web :5099 + API :5100 localmente contra `p1_dev` com `MigrationMode=Disabled`.
- Driver reproduzível `scripts/gate-b-saas-runtime.ps1` → **26/26 PASS, 0 FAIL**. Ao vivo: (1) cadeia Web→bearer — `GET /Auth/ApiToken` minta token bruto de 64 chars; cookie cru `SIGOV.AUTH` como bearer → 401; anônimo → 401 `authentication_required`; (2) avaliador canônico fail-closed A(ATIVA→200)/B(SUSPENSA→403 `BLOQUEADO_COMERCIAL`)/C(expirada→403 `CONTRATO_EXPIRADO`)/D(limite=1→200), todos com `login.ok=True`; (3) 403 padronizado (`motivo`) paridade Web+API — `/Seguranca/Usuarios`: perfil restrito → 403 `SEM_PERMISSAO` ainda com `/MinhaCentral` 200, admin global → 200; (4) revogação por alteração de identidade paridade A5 — bump em `usuario.updated_at` → Web `/MinhaCentral` 302 + API com token antigo 401, estado restaurado; (5) admin global MNSOFT como autoridade do banco — tripla `saas.plataforma.administrar` (id 1755, ativo=t, módulo saas), 8 holders ativos.
- Consolidação dual-catalog (`f388b67d`): navegação renomeada `ICommercialModuleCatalogService`; decisão permanece somente em `PersistentModuleCatalogService` (banco autoridade, fail-closed). Build `-warnAsError` 0w/0e; suítes preservadas Unit 508 / Integration 158 / Api 158 (0 ignorados).
- Estado final: `p1_dev` preservado e utilizável (fixture idempotente; `updated_at` do cenário D restaurado a `'null'`); forwarder/apps mantidos para os gates seguintes; serviço Postgres do Windows intocado. Artefatos bankados em `docs/evidencias/rcevo/gate_b_saas_aut/`.
- Evidência: `docs/evidencias/rcevo/relatorio_gate_b_saas_aut_runtime_pg16.md`; matriz P1 → PASS, BACKLOG item 6 → VALIDADO. Resíduos (regra 13): acesso cruzado em nível de API (`TenantAccessGuard`) e mutações comerciais/convites/troca de contexto com auditoria ainda só cobertos deterministicamente pelas suítes. Próximo item exato: **Gate C (Indústria OP)**. GED por último.

---

Data: 2026-10-09. P0.4 — jornada de autenticação/isolamento ao vivo no PG16 oficial. Estado: **PASS (evidência ao vivo)** → **P0 integralmente verde**.

- Host: worktree `swift-circuit`, HEAD `27ae43ef`; SDK .NET 10; Docker/PostgreSQL 16 disponíveis. Banco descartável `p04_dev` no container oficial `sigov-postgres` (PostgreSQL **16.14**), com seeds dev + fixture idempotente de um 2º tenant `p04-b` (usuários PF `p04pf`/CPF e PJ `p04pj`/CNPJ, mesma senha dev PBKDF2).
- Nota de ambiente (regra 13/18, não mascarada): a porta `5432` do host era ocupada por um Postgres 18 nativo do Windows (`postgresql-x64-18`), sombreando o mapeamento do Docker; a conexão de `localhost:5432` retornava o banco do Windows. Como não havia elevação para parar o serviço, garantiu-se o **PG16 oficial** via forwarder temporário `alpine/socat` na porta livre `5433` → `sigov-postgres:5432` (rede `sigov-plus_default`); conexão confirmou `16.14 … db=p04_dev`.
- Itens P0.4 provados ao vivo (fluxo real do Web: form + antiforgery + cookie): (1) login por **e‑mail** → `/MinhaCentral` + sessão em `sigov.identidade_sessao`; (2) login por **CPF** `11144477735` → `p04pf`/t7 (via `pessoa_fisica.cpf`; antes do registro `CandidateCount=0`); (3) login por **CNPJ** `11222333000181` → `p04pj`/t7 (via `entidade.cnpj`); (4) **senha errada** → falha explícita sem sessão; (5) **Minha Central** autenticada no contexto do próprio tenant (sem vazamento entre tenants); (6) **logout** revoga (`motivo_encerramento='LOGOUT'`); (7) **dois tenants** isolados (t6 `sigov-local` vs t7 `p04-b`); (8) **tenant suspenso** bloqueia login e religa (`ativo=false→true`); (9) **revogação por alteração de identidade** (paridade A5: bump em `usuario.updated_at` → `auth_version` obsoleta → cookie rejeitado → redirect p/ login). Rastro completo em `sigov.auditoria_evento`.
- Limpeza: `p04_dev` dropado (residuo zero), `p04fwd` removido, Web local finalizado; serviço Postgres do Windows intocado.
- Evidência: `docs/evidencias/rcevo/relatorio_p0_4_jornada_autenticacao_isolamento_pg16.md`; promovido na matriz P0.4 → PASS. Próximo item exato: **Gate B (SaaS)**. GED por último.

---

Data: 2026-10-09. P0.3 — Swagger e auditoria integral de rotas. Estado: **PASS (evidência ao vivo)**.

- Prerrequisitos P0.1/P0.2 verdes (este host, HEAD `6c7b269a`).
- Auditoria de conflitos de rotas passou em duas camadas:
  - Testes **executados** que sobem um host real via `WebApplicationFactory` (`tests/Sigov.ApiTests/ApiContractRegressionTests.cs`, fixture `SigovApiFactory`): `SwaggerJson_Deve_Ser_Valido_Sem_Rotas_Ou_OperationIds_Duplicados` (GET `/swagger/v1/swagger.json` → HTTP 200, `application/json`, `openapi` presente, sem operationIds/rotas duplicadas) e `ApiDescriptions_Nao_Devem_Conter_Metodo_E_Caminho_Duplicados` (ApiExplorer sem método/caminho duplicado). Parte dos ApiTests 158/158 verdes.
  - Auditoria estática `bash scripts/check-api-route-conflicts.sh` → "Nenhum conflito direto em **637** rotas API".
- Nota: o container `sigov-api` do stack pré-existente estava em loop de reinício (estado ambiente, não montado por esta execução); a evidência de Swagger runtime vem do teste executado, não do container.
- Promovido na matriz: `MATRIZ_FLUXOS_VERTICAIS.md` P0.3 → PASS.
- Próximo item exato: **P0.4** — jornada de autenticação/isolamento no banco oficial (login CPF/CNPJ/e-mail, Minha Central, logout, revogação, tent suspenso e dois tenants); só então Gate B (SaaS). GED por último.

---

Data: 2026-10-09. P0.2 — build e testes realmente executados. Estado: **PASS (evidência ao vivo)**.

- Host: worktree `swift-circuit`, HEAD `6c7b269a`; SDK .NET 10.0.400; Docker/PostgreSQL 16 disponíveis.
- Suítes executadas sem teste em silêncio (nenhum `Skip=`/ignore em `tests`): UnitTests **508/508**, IntegrationTests **158/158**, ApiTests **158/158**. Os 12 contratos estáticos históricos de ApiTests que a matriz antiga marcava como falhando **já não falham** (ApiTests integralmente verde). Todos os 3 test projects participam do `sigov.sln`.
- Build Release `-warnaserror`: **0w/0e**.
- Nota de ambiente (regra 13/18, não mascarada): o `packages.lock.json` de `Sigov.Api` grava o alvo `net10.0/linux-x64` embora o projeto não declare `RuntimeIdentifier`; por isso `dotnet restore sigov.sln --locked-mode` reporta **NU1004** em host não-linux (CI linux-x64 bate). Evidência de compilação obtida com restore padrão + `-warnaserror`.
- Promovido na matriz: `MATRIZ_FLUXOS_VERTICAIS.md` P0.2 → PASS.
- Próximo item exato: P0.3 auditoria integral de conflitos de rotas (`bash scripts/check-api-route-conflicts.sh`, 630 rotas) e P0.4 jornada de autenticação/isolamento no banco oficial (login CPF/CNPJ/e-mail, Minha Central, logout, revogação, tent suspenso, dois tenants); só então Gate B (SaaS). GED por último.

---

Data: 2026-10-09. RC-EVO-B §6 + fechamento P0.1/Gate A item 6. Estado: **VALIDADO COM EVIDÊNCIA (build/suítes + PostgreSQL 16 ao vivo)**.

- Baseline: worktree `swift-circuit`, HEAD `6c7b269a`, árvore limpa. SDK .NET 10.0.400 (`global.json` 10.0.100 + latestFeature); Docker 29.4.3 e PostgreSQL 16.14 (`postgres:16`, container `sigov-postgres`) disponíveis neste host.
- Parte 1 — RC-EVO-B §6 (contexto financeiro da sessão na integração ponto→folha): regra fail-closed `FolhaPontoFinanceiraRegras.ValidarContextoFinanceiro` + recusa nomeada `CONTEXTO_FINANCEIRO_AUSENTE` **antes** de materializar (a fila nunca é pulada com sucesso falso quando entidade/exercício não estão resolvidos na sessão) já estava commitada; desta execução validou a suíte completa e registrou a evidência que o commit "ajutes novos" não trazia. Sem migration/permissão/interface.
- Gates da Parte 1: build Release `-warnaserror` **0w/0e**; UnitTests **508/508**; IntegrationTests **158/158**; ApiTests **158/158**; manifest JSON válido (220 entradas, última `20261008130000`, 4 excluídas do baseline). Nota: restore `-locked-mode` acusou `NU1004` por RID do lockfile ser `linux-x64` (artefato de ambiente deste host); evidência de compilação obtida com restore padrão + `-warnaserror`.
- Parte 2 — fechamento do item 6 (gap RC50.68A / P0.1): executou-se o fluxo oficial do runner (`database/docker/apply-migrations.sh` corrente, reconciliação bidirecional §6) **ao vivo no PG16** em duas cenas com bancos descartáveis:
  - Instalação limpa (`evo_c_clean`, vazio): exit 0; canônico 216/última `20261008130000`; sidecar 217 (=216+marker por design); divergências lado-a-lado **0**; canônico ⊆ sidecar.
  - Upgrade legado de `7ece53c1` (`evo_c_legacy`, canônico 212/última `20261007110000`, marker semeado no shape legado): fase 1 pulada, aplicadas exatamente as 4 pendentes (`20261007120000`,`20261007130000`,`20261008120000`,`20261008130000`) e **reconciliadas no canônico com `source='docker-sidecar'`** (o defeito original: canônico ficava para trás); canônico 212→216; divergências **0**.
  - Sem `ERROR: column "version" does not exist`; teardown `DROP ... WITH (FORCE)` e residuo zerado verificado.
- Promovido na matriz: `MATRIZ_FLUXOS_VERTICAIS.md` P0.1 → item 6 FECHADO; comando normativo apontando para os sub-itens P0 restantes (P0.2/P0.3/P0.4) e só então Gate B.
- Evidências: `docs/evidencias/rcevo/relatorio_s6_contexto_financeiro.md` e `docs/evidencias/rcevo/relatorio_p0_1_fechamento_item6_pg16.md`; logs em `Temp\opencode\` (`evo_c_faseA_clean.log`, `evo_c_faseB_upgrade.log`).
- Próximo item exato: retomar os sub-itens P0 ainda PARCIAIS (P0.2 conferir os contratos estáticos históricos de ApiTests no estado atual — hoje 158/158 verdes; P0.3 auditoria integral de conflitos de rotas; P0.4 jornada login/CPF/CNPJ/logout/revogação/tent suspenso/dois tenants no banco oficial); somente após P0 integralmente verde, Gate B (SaaS canônico). GED permanece por último.

---

Data: 2026-09-22. RC51.03A — retry concorrente do recebimento de compras. Estado: **CORRIGIDO ESTATICAMENTE / GATE RUNTIME BLOCKED**.

- Baseline: branch `work`, HEAD inicial `7443a8a4b7cb62173e6bb28083dbdaca9ffd3a77`, árvore limpa. O fluxo selecionado foi rastreado da navegação de Compras Empresariais às rotas MVC, application service, repositório Dapper, tabelas de recebimento/eventos e efeitos em saldo/movimento de estoque.
- Defeito confirmado por inspeção da ordem transacional: dois comandos simultâneos com a mesma chave podiam ambos observar ausência antes do lock do pedido; após o primeiro commit, o segundo avaliava a versão já incrementada e falhava, em vez de devolver o recebimento persistido. O repositório agora repete a leitura da chave após adquirir o lock e antes de validar estado/versão.
- O teste de regressão estático foi acrescentado à classe existente `PostBuild01RegressionTests` e exige as leituras de idempotência antes e depois de `FOR UPDATE`; nenhuma classe de teste nova foi criada. Não houve alteração de schema, migration, scripts consolidados, permissão, regra financeira ou interface.
- PASSOU: catálogo/paridade estática das migrations, conflitos de rotas, colisões SQL, artefatos rastreados, sintaxe Bash/JSON, JavaScript e `git diff --check`.
- **BLOCKED:** restore, build, teste .NET, PostgreSQL 16, concorrência real, dois tenants, Web/API, navegação e capturas; não há `dotnet`, `psql`, PowerShell, connection string ou aplicações iniciadas. A correção não é declarada homologada.
- Próximo incremento: provisionar o Gate A, executar duas confirmações simultâneas da mesma chave e chaves distintas sobre o mesmo saldo, e só então concluir a decisão de inspeção/devolução/estorno já registrada no backlog RC51.03.

---

Data: 2026-09-16. RC51.03 — recebimento parcial de compras empresariais. Estado: **IMPLEMENTADO SEM HOMOLOGAÇÃO RUNTIME**.

- Central real com filtros, paginação no servidor, ordenação estável e totais tenant-scoped; formulário por pedido com destino nominal, quantidades e rastreio opcional.
- Confirmação serializa pedido/itens, bloqueia excesso e estado incompatível, é idempotente, audita e grava entrada no estoque canônico apenas para quantidade aceita. Item sujeito a inspeção permanece em conferência e indisponível.
- Migration forward-only `20260916120000` sincronizada com manifesto e scripts; novas tabelas usam bigint identity e preservam os UUIDs legados.
- **BLOCKED:** .NET, PostgreSQL 16 e navegador ausentes; build, testes runtime, concorrência e evidências visuais não executados. Documento e matriz: `docs/entregas/RC51-03-RECEBIMENTO-COMPRAS.md`.
- Próximo item exato: executar Gate A e cenários PostgreSQL; depois concluir decisão de conferência, devolução e estorno com verificação de movimentos posteriores.

---

Data: 2026-09-16. Correção de nulabilidade do ciclo contratual SaaS. Estado: **CORRIGIDA ESTATICAMENTE / GATE RUNTIME BLOCKED**.

- Causa dos dois `CS8602`: `QuerySingleOrDefaultAsync<ContractRow>` admite ausência; a guarda composta para suspensão/reativação não estreitava `before` para as guardas posteriores de vigência e cancelamento. O fluxo agora entra em um bloco de mutação de contrato existente, retorna antes de qualquer `UPDATE` quando a linha não existe e usa a variável local não nula `currentContract`, sem `!`, `?.`, pragma ou mudança do contrato público.
- Semântica preservada: entrada inválida, módulo ausente, tenant ausente/inativo, tenant suspenso/cancelado, dependência ausente, contrato ausente, estado incompatível, versão concorrente, vigência encerrada e cancelamento efetivado falham antes da gravação. O controller continua negando falta de identidade/permissão e acesso de administrador local a outro tenant antes de chamar o serviço.
- Matriz: contratar → SuperAdmin autorizado → tenant ativo e módulo canônico/dependências válidos → insere contrato e auditoria → `CONTRATADO`; suspender → SuperAdmin autorizado → contrato vigente em estado elegível e versão atual → preserva condições, audita → `SUSPENSO`; reativar → SuperAdmin autorizado → contrato `SUSPENSO`, tenant ativo, vigência/cancelamento elegíveis e versão atual → preserva condições, audita → `HABILITADO`.
- Formulários no escopo: lista/detalhe são `GET` autorizados e leem via Dapper; contratar, suspender e reativar são `POST` com antiforgery, autorização persistente, validação no serviço, transação, auditoria e redirecionamento sem afirmar sucesso quando o resultado falha. Não houve alteração visual nem de schema nesta correção; o template e as jornadas persistidas existentes foram preservados.
- Teste de regressão existente foi ampliado, sem criar classe, para exigir a guarda explícita e impedir o retorno do operador de supressão. `git diff --check` e varredura de conflitos passaram.
- **BLOCKED:** `dotnet restore/build/test`, compilação Razor, PostgreSQL 16, navegador e capturas não foram executados porque este container não dispõe de `dotnet`, `psql`, Docker nem navegador. A entrega não é declarada homologada.
- Risco restante e próximo item exato: instalar o SDK `10.0.100` e disponibilizar PostgreSQL 16 isolado; executar os gates da solução e provar contratação/suspensão/reativação, concorrência, rollback, duplicidade, dois tenants, permissão negada, sessão revogada e preservação do espelho legado; depois executar a jornada autenticada e as seis larguras.

---

Data: 2026-09-15. Correção do ciclo contratual SaaS. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

| Item inspecionado | Classificação | Evidência/limite |
|---|---|---|
| Catálogo e contratação | parcial | `modulo_saas` e `tenant_modulo_contratado` são autoridades persistidas; catálogo Commercial legado ainda é consumidor de navegação, não autoridade contratual |
| Administração de tenants/SuperAdmin | implementado sem validação | pesquisa, filtros, paginação, detalhe, vínculos e contratos usam Dapper; runtime indisponível |
| Usuários, vínculos, perfis e permissões | parcial | identidade/sessão e autorização persistidas existem; jornada completa de convite e limite concorrente não foi homologada |
| Sessões e avaliador | implementado sem validação | validação de sessão e avaliador persistente preservados; mudança P0/P1 fora desta correção |
| Sidebar, layout e formulários | parcial | shell compartilhado preservado; confirmação contratual agora explicita cliente, módulo, efeito e consequências |
| Auditoria de formulários | bloqueado | 591 candidatos e zero jornadas homologadas conforme checkpoint vigente |
| Manutenção, inspeção e não conformidades | parcial/ausente | execução e histórico de OS parciais; inspeção e não conformidade ligadas à OS continuam ausentes |

- Estado inicial: branch `work`, HEAD `f1b8a87bcc50aec60e4a67d25a9493e5815631bf`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `psql`, Docker e navegador ausentes.
- Fonte canônica confirmada: leitura/escrita contratual em `tenant_modulo_contratado`; `tenant_modulo` permanece projeção legada unidirecional; catálogo comercial contratável em `modulo_saas`; histórico em `tenant_modulo_contratado_historico`; autorização em identidades/vínculos/perfis/permissões persistidos e no avaliador único.
- Correção: contratação repetida deixa de sobrescrever contrato/histórico; suspensão exige estado elegível; reativação exige estado `SUSPENSO`, tenant ativo, dependências, vigência ainda elegível e cancelamento não efetivado. Suspender/reativar preserva a vigência original; reativar não renova contrato nem quita cobrança.
- Interface: confirmações mostram cliente, módulo, momento e consequências; a revisão esclarece que dependências não são incluídas silenciosamente e contratação não concede permissão.
- Sem migration: o schema canônico existente foi reutilizado. Sem alteração do avaliador, troca de contexto ou dashboard.
- **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16, concorrência real, dois tenants e screenshots nas seis larguras não foram executados por ausência das ferramentas.
- Riscos restantes: snapshot de preço/periodicidade e fluxo cliente solicitar → SuperAdmin aprovar ainda precisam ser unificados com a solicitação comercial canônica; cancelamento contratual não foi acrescentado sem decisão formal de política.
- Próximo item exato: integrar `saas_solicitacao_cliente` ao detalhe canônico para aprovação idempotente que grave preço/periodicidade aceitos em `tenant_modulo_contratado`, sem ativação direta pelo administrador do cliente.

---

Data: 2026-09-15. Execução de OS — progresso verificável e histórico. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

| Jornada solicitada | Classificação comprovada | Evidência e limite |
|---|---|---|
| Login e Minha Central | implementada sem validação | rotas e serviços canônicos preservados; runtime indisponível |
| Criar, consultar e atribuir OS | parcial | API/Dapper existentes; seletores autorizados e prova PostgreSQL pendentes |
| Executar OS atribuída | parcial | técnico vinculado ao usuário é conferido no `UPDATE`; estado e versão são condições persistidas |
| Salvar checklist da execução | parcial | vazio permanece pendente, domínio fechado é validado no servidor e concorrência usa `version`; modelos versionados ainda ausentes |
| Histórico da OS | implementada sem validação | nova leitura tenant-scoped de `os_status_historico`, API autorizada e linha do tempo na execução |
| Inspeções e revisão | ausente | não foi identificada implementação canônica completa de manutenção; não foi criado atalho paralelo |
| Não conformidades e ações corretivas | ausente | estruturas de Qualidade não comprovam esta jornada ligada à OS; permanece backlog |
| Materiais e custos | parcial | serviço OS usa saldo canônico e transação; homologação concorrente PostgreSQL continua pendente |
| GED | por último | nenhuma funcionalidade GED foi criada ou antecipada |

- Contrato aplicado ao checklist: usuário autenticado do tenant deve corresponder ao `usuario_id` do técnico atribuído; a OS deve estar em `AGENDADA`, `EM_DESLOCAMENTO`, `EM_EXECUCAO` ou `PAUSADA`; item, OS e versão precisam coincidir. Resposta vazia salva rascunho pendente, resposta de domínio fechado inválida falha, gravação incrementa a versão e repetição com versão antiga é recusada.
- Contrato aplicado à conclusão já existente e preservado: somente a transição permitida pelo estado é aceita; checklist obrigatório bloqueante e apontamento aberto impedem conclusão; atualização e histórico compartilham transação e conflito de versão faz rollback.
- Interface: detalhe ganhou breadcrumb, orientação “Como usar”, progresso real, estado explícito de salvamento e histórico cronológico. Conteúdo persistido é codificado antes de inserção no HTML.
- Preflight: branch `work`, HEAD inicial `bf0911449f983c81be677241c478c4fbba1704de`, árvore limpa e nenhum remoto/upstream configurado. SDK .NET 10 e PostgreSQL/psql ausentes; Node 20 e Git disponíveis.
- **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16 vazio/upgrade/reexecução, dois tenants, concorrência real e capturas nas seis larguras não puderam ser executados sem runtime e navegador. Nenhuma jornada foi declarada homologada.
- Próximo item exato: criar, em migration aditiva, o modelo de checklist versionado canônico e a instância imutável vinculada à OS; depois implementar inspeção/revisão e somente então não conformidade/ação corretiva. GED permanece por último.

---

Data: 2026-09-15. Evolução vertical de Patrimônio — cadeia de custódia. Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

| Funcionalidade | Estado real | Evidência | Lacuna | Ação desta execução |
|---|---|---|---|---|
| Template/navegação | Compartilhado, não homologado | `_Layout`, `_Sidebar` e componentes existentes | navegador ausente; catálogo nominal patrimonial incompleto | detalhe ganhou breadcrumb, hierarquia, estado vazio, foco e ação protegida |
| Educação — matrícula/frequência | Parcial | serviços Dapper e testes estáticos existentes | transferência e correção auditada ainda pendentes | preservada, sem promoção |
| Patrimônio — movimentação | Parcial, agora com cadeia consultável | serviço transacional, API/Web e histórico real | solicitação/recebimento e seletores nominais ainda pendentes | consulta tenant-scoped, idempotência por operação e origem serializada |
| Patrimônio — inventário | Parcial | abertura, conferência e fechamento persistidos | fotografia temporal e movimentos durante conferência pendentes | preservado, sem promoção |
| Compras/Almoxarifado/Indústria/SaaS | Regressões runtime aguardam Gate A | matrizes e testes existentes | SDK, PostgreSQL e navegador ausentes | nenhuma implementação paralela criada |

- Preflight: branch `work`, HEAD inicial `e88132d578ea90e6964f64efe0a29354391f4fdd`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `psql`, PostgreSQL, PowerShell e navegador ausentes; Node 20 disponível.
- Patrimônio: o detalhe passa a retornar e exibir o histórico persistido por tenant. A movimentação bloqueia destino vazio/inalterado, serializa a origem com `FOR UPDATE`, aceita chave idempotente estável (`Idempotency-Key` na API e campo por formulário), recusa reutilização com conteúdo divergente e conserva atualização, evento e auditoria na mesma transação.
- Interface: status inclui texto, histórico tem estado vazio e tabela responsiva, formulário tem labels, orientação, confirmação, “Como usar”, foco visível e bloqueio contra duplo envio. Não houve alteração de schema.
- Validação estática: JavaScript, JSON e `git diff --check` passaram. **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16, regressões integradas e capturas reais nas seis larguras, pois as ferramentas não estão instaladas. Nenhum módulo foi promovido.
- Riscos restantes: IDs de unidade/responsável ainda são entrada técnica e precisam de catálogo autorizado; fluxo pendente confirmar/receber e concorrência por versão exigem migration própria; inventário ainda não preserva fotografia temporal.
- Próximo item exato: criar o fluxo patrimonial persistido SOLICITADO → RECEBIDO/CANCELADO, com seletores autorizados e versão do bem; depois corrigir fotografia temporal do inventário. GED permanece por último.

---

Data: 2026-09-15. RC51.02Q (integridade do rascunho de compras). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `47b51a7a99888036dc1d3182b33d77121dc28a0c`, árvore limpa, sem remoto/upstream; SDK normativo `10.0.100`. `dotnet`, `pwsh`, `psql`, Docker e navegador permanecem ausentes.
- Regressões: as URLs canônicas `/Governanca/QualidadeDados` e aliases distintos já estavam corrigidos e protegidos por `WebRuntimeSmokeTests`; `PagedResult.TotalItems`, Razor, nullable industrial e Minha Central também permanecem corrigidos no checkout. A requisição com host real ficou BLOCKED pela ausência do runtime.
- Compras: validação de rascunho passou a ser equivalente no cliente e no servidor para justificativa, urgência, tipo, descrição, unidade, quantidade, valor, limite e duplicidade de itens. A criação conserva uma única chave de idempotência durante retry/timeout. O editor ganhou labels visíveis por item e agrupamento semântico responsivo.
- Validação estática: JavaScript, JSON, shell e `git diff --check` passaram. **BLOCKED separadamente:** restore/build/Razor/testes/Swagger, PostgreSQL 16, host Web, screenshots e cenários concorrentes, pois as ferramentas necessárias não estão disponíveis.
- Escopo: rascunho/listagem/detalhe/envio permanecem **PARCIAIS, implementados sem validação runtime**; aprovação configurável, pedido, recebimento/estorno e integração completa com estoque/Indústria permanecem **AUSENTES ou AGUARDA_GATE**. Nenhum módulo foi promovido. GED permanece por último.
- Próximo item exato: executar Gate A com SDK .NET 10 e PostgreSQL 16; depois persistir a política canônica de alçadas e implementar decisão concorrente sem autoaprovação, antes de pedido e recebimento.

---

Data: 2026-09-14. RC51.02O (governança estática do manifesto). Estado: CORRIGIDA ESTATICAMENTE / BLOCKED.

- Preflight: branch `work`, HEAD inicial `b710b05b188edbacee1cb9b30a8e9428fdc00cce`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `pwsh`, `psql`, Docker e navegador ausentes.
- Defeito confirmado: a migration `20260914120000` estava intacta, mas manifesto e consolidados registravam o hash do conteúdo sem a quebra final, divergindo do algoritmo normativo. O checksum foi sincronizado nos sete catálogos e o valor anterior preservado como `knownChecksums` com pós-condição; nenhuma migration publicada foi editada.
- Aplicadores Bash e PowerShell agora recusam metadados históricos/probes malformados antes de DDL. Teste existente ampliado; validações Bash/JSON/diff passaram. O CS8629 industrial já permanece corrigido com pattern matching e HTTP 422 para contexto nulo.
- Sem alteração visual ou schema; screenshot não aplicável. **BLOCKED:** PowerShell ValidateOnly, restore/build/test, OpenAPI, navegador e provas PostgreSQL 16 pela ausência das ferramentas.
- Evidência e matriz: `docs/entregas/RC51-02O-GOVERNANCA-MANIFEST.md`.
- Próximo item exato: executar o ValidateOnly PowerShell e provar no PostgreSQL 16 vazio/reapply/histórico conhecido e desconhecido/upgrade legado; depois validar login e isolamento antes do Gate B SaaS.

---

Data: 2026-09-14. RC51.02N (nullable da integração industrial e clareza operacional). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `84214791f4f4f42ea6ff106225e2932429ae31ad`, árvore limpa, sem remoto/upstream. O SDK normativo é `10.0.100`; `dotnet`, PostgreSQL, Docker e navegador não estão disponíveis.
- Causa do CS8629: após testar `HasValue`, `GerarOsAsync` voltava a ler `_tenant.EntidadeId.Value` e `_user.UsuarioId.Value` dentro de expressões posteriores; a análise nullable não preservava a garantia. `null` significa contexto operacional autenticado incompleto e agora retorna 422 antes de entitlement, consulta ou gravação. Pattern matching captura `entidadeId` e `usuarioId`, usados no avaliador e SQL, sem identidade padrão.
- Regressão: a classe existente `PostBuild06IndustriaTests` protege a validação, a mensagem contratual, o reuso dos valores tipados e a ausência de `.Value`/`?? 0` neste fluxo. Registro ausente/outro tenant, esfera inconsistente, falta de permissão e idempotência continuam protegidos pelos predicados e testes existentes.
- Interface: a página industrial compartilhada explicita a sequência ordem → materiais/reserva → apontamento → qualidade → encerramento, inclui “Como usar” e não converte erro de consulta em lista vazia. A matriz curta e as transições foram registradas em `docs/industria-producao.md`; o módulo permanece `PARCIAL`.
- Sem alteração de schema ou migration. **BLOCKED:** restore/build/test, OpenAPI, login/Minha Central, jornada no navegador, screenshots e provas PostgreSQL 16 não foram executados pela ausência das ferramentas/runtime.
- Próximo item exato: implementar a unidade transacional e idempotência por conteúdo entre reserva/consumo/produção da OP e estoque canônico, seguida da reconciliação concorrente de qualidade, entrada e custo no encerramento; provar em PostgreSQL 16 duas reservas, retries e encerramento concorrente com apontamento. GED permanece por último.

---

Data: 2026-09-14. RC51.02M (aplicador Bash fail-closed). Estado: CORREÇÃO ESTÁTICA / BLOCKED.

- Preflight: branch `work`, HEAD inicial `158cef1116d2b9cc9be28757ec5ea8fc90d5fd9c`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; `dotnet`, `pwsh`, `psql`, Docker e navegador ausentes. **Erro específico não fornecido** na tarefa.
- Matriz curta: Fundação | havia wrapper Bash que validava apenas migrations e executava todos os SQLs sem ledger/pós-condições | reaplicação e upgrade podiam alterar o banco parcialmente | normalização/checksum de migrations e compatibilidades, seguida de bloqueio explícito da execução insegura | `VALIDATE_ONLY=true`, cenários inválidos sintéticos, `bash -n` e contrato na classe de regressão existente. API/Web, Minha Central, Educação, Saúde/ACS, Jurídico e GED | preservados | Gate A runtime continua ausente | nenhuma evolução funcional posterior iniciada | restore/build/runtime/PG16 permanecem BLOCKED.
- Causa confirmada por inspeção do primeiro executor afetado: `apply-migrations-manifest.sh` ignorava `compatibilityBefore`, `compatibilityAfterAll`, ledger, `knownChecksums` e pós-condições; ainda calculava hash com normalização diferente do runner canônico. A execução DDL foi removida do wrapper até que alcance paridade integral. O modo estático agora aceita BOM UTF-8, normaliza CRLF/CR, valida existência, confinamento e checksum das compatibilidades e recusa duplicidade por migration.
- Sem alteração de schema, migration, manifesto ou scripts consolidados. Nenhuma jornada posterior foi promovida, e não há screenshot porque não houve mudança visual e o navegador/runtime estão indisponíveis.
- Próximo item exato: executar `pwsh -NoProfile -File scripts/apply-migrations-manifest.ps1 -ValidateOnly`; em PostgreSQL 16, provar banco vazio/reaplicação, rejeição de versão/checksum desconhecidos, `knownChecksums` com pós-condição e upgrade legado formal. Implementar execução Bash somente com o mesmo ledger, atomicidade e pós-condições do runner canônico.

---

Data: 2026-09-14. RC51.02L (integridade mínima de matrícula e frequência). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `dd8b2e08f7252d89b8c9c6ff5083d5de791b4fa3`, árvore limpa, sem remoto/upstream; projetos e migrations confirmados. SDK normativo `10.0.100`; `dotnet`, `psql`, Docker e navegador ausentes. O download do SDK foi tentado e bloqueado pelo proxy HTTP 403.
- Lacuna confirmada: a matrícula era inserida antes da tentativa de incrementar a ocupação e o resultado do `UPDATE` era ignorado. Assim, a disputa pela última vaga podia confirmar duas matrículas e divergira de `vagas_ocupadas`. Identificadores de aluno/escola/ano/turma também não eram validados conjuntamente no contexto. Frequência aceitava aluno sem matrícula elegível e data fora da vigência.
- Implementado: a mesma transação agora reserva exatamente uma vaga antes do insert, sob `UPDATE` concorrente, validando tenant, entidade, cadastros ativos, compatibilidade escola/ano/turma e período não encerrado. Zero linhas atualizadas aborta e faz rollback. O lançamento de frequência passou a ser `INSERT ... SELECT` condicionado a matrícula ativa/confirmada e data entre ingresso e fim do ano letivo; situação e justificativa são validadas no serviço. Ausência de entidade não usa mais o contexto fictício `1`.
- Matriz: Educação permanece **PARCIAL / IMPLEMENTADA SEM VALIDAÇÃO RUNTIME**; Saúde/ACS e Jurídico foram apenas inspecionados e permanecem **PARCIAIS**, sem alteração nesta fatia. SaaS, Compras, Estoque, Indústria, Frota, Contratos e GED foram preservados; GED continua por último.
- Testes estáticos existentes foram ampliados para proteger a ordem reserva→insert e os predicados de contexto/elegibilidade. `git diff --check` passou. **BLOCKED:** restore/build/test, PostgreSQL 16 vazio/upgrade/reexecução, OpenAPI, login/Minha Central/logout, jornadas HTTP e screenshots responsivos não foram executados por ausência de ferramentas/runtime.
- Riscos restantes: os índices históricos de matrícula ainda impõem unicidade ampla por aluno/ano e precisam de migration corretiva sincronizada para representar oferta; transferência ainda só altera o estado da matrícula, sem criar novo vínculo preservando o anterior; não existe operação específica de correção auditada da chamada. Esses pontos impedem o aceite vertical completo.
- Próximo item exato: implementar transferência atômica com histórico e nova matrícula por oferta, acrescentar migration forward-only que substitua a unicidade genérica e sincronizar manifesto/consolidados; aceite em PostgreSQL 16: uma vencedora na última vaga, oferta complementar legítima, transferência preservando frequência anterior e retry sem duplicação.

---

Data: 2026-09-14. RC51.02K (correção Razor e paginação de uso SaaS). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Projeto confirmado: `src/Sigov.Web/Sigov.Web.csproj`; branch `work`, HEAD inicial `e23e8c18a1af8e5c07386fb5cb019c6c4a47a87b`, árvore inicialmente limpa. O SDK normativo permanece .NET `10.0.100` e nenhuma alteração foi feita no Agro360.
- Causa Razor confirmada: `Uso.cshtml` é uma View MVC e declarava a variável local `page`; a expressão implícita `@page` no texto da paginação era interpretada pelo parser como a diretiva Razor reservada. Não existia motivo para adicionar a diretiva `@page` nem converter a tela em Razor Page. A variável passou a `pageNumber` e a saída textual usa expressão explícita.
- Evolução verificável: paginação de `auditoria_evento` passou ao PostgreSQL, com `limit/offset`, total independente da página e ordenação estável por instante e identidade; páginas fora do intervalo retornam à última válida. Cliente, módulo, situação e período são preservados nos links e na exportação; cliente agora é escolhido pelo catálogo retornado pelo servidor, não por digitação de ID.
- Sem números fictícios: total e última atividade vêm do histórico persistido; usuário ativo continua explicitamente indisponível porque o schema atual não tem evento canônico de uso por identidade. A interface explica período UTC, significado da métrica, limitações de retentativa e diferença entre indisponibilidade e ausência de registros.
- Autorização existente preservada: SuperAdmin continua avaliado pelo serviço canônico e administrador local é restringido ao próprio `tenant_id`; a exportação reutiliza filtros e avaliação do endpoint. Nenhuma migration ou mudança estrutural foi necessária.
- Validação estática: busca contextual das demais Views encontrou `page` apenas dentro do JavaScript de Indústria, sem colisão Razor; balanceamento dos elementos principais e `git diff --check` passaram. **BLOCKED:** `dotnet restore`, `dotnet build`, `dotnet test`, renderização HTTP, PostgreSQL 16 e screenshot não foram executados porque `dotnet`/runtime/banco/navegador não estão disponíveis neste ambiente.
- Próximo item exato: instalar o SDK `10.0.100`, executar restore/build/test e validar `/SaasAdmin/Uso` com PostgreSQL 16 para tenant global, administrador local, filtros combinados, página excedente e exportação; depois instrumentar atividade por usuário somente quando houver evento canônico idempotente.

---

Data: 2026-09-14. RC51.02J (SaaS Admin — uso verificável). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `8f07ab1c54d79d85237f4fdb11725d73010a09b2`, árvore limpa, sem remoto ou upstream configurado.
- Problema | Causa | Serviço reutilizado | Correção | Evidência: Uso exibia números e um município demonstrativos | `Uso.cshtml` era uma cópia estática e `saas.uso.js` não implementava consulta | `ISuperAdminOperationalDashboardService`/`auditoria_evento` | filtros de cliente/período/módulo, paginação, exportação existente e indisponibilidade explícita | controller e view recebem `SuperAdminOperationalDashboard`; script impede duplo envio.
- Problema | Causa | Serviço reutilizado | Correção | Evidência: script de clientes enviava `X-Sigov-Tenant: municipio-demo` | contexto fixo no navegador | avaliador de autorização e contexto já aplicados pelo controller | cabeçalho removido; servidor permanece autoridade | `saas.tenants.js` não resolve nem escolhe tenant.
- Métrica entregue: “operações concluídas” conta os eventos persistidos em `auditoria_evento`, no intervalo UTC inclusivo consultado; não soma login, visualização, decisão de autorização ou falha. Não há deduplicação adicional na leitura: retries só não inflam o resultado quando a operação canônica não cria novo evento. Usuários ativos, limites e uso por módulo permanecem “medição indisponível” enquanto não houver evento canônico verificável.
- Preservado: consultas Dapper parametrizadas, autorização global/local existente, isolamento pelo `tenantId` autorizado, catálogo e contratos canônicos e exportação protegida. Sem alteração de schema ou migration.
- Validação estática: `node --check` nos dois scripts, conflito de rotas API e `git diff --check` passaram. **BLOCKED:** restore/build/test, PostgreSQL 16, runtime autenticado e screenshots responsivos, pois SDK, banco e navegador não estão disponíveis neste ambiente.
- Próximo item exato: instrumentar uma chave idempotente canônica na conclusão de uma operação já auditada e provar, em PostgreSQL 16, que retry mantém um evento; aceite: filtro de dois clientes não mistura eventos e exportação contém exatamente a mesma janela autorizada da tela.

Data: 2026-09-11. RC51.02I (onboarding contextual persistido). Estado: IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED.

- Preflight: branch `work`, HEAD inicial `75bdee0d096d6c948f4fea9e21757eb7f6c20420`, árvore limpa e sem remoto/upstream.
- Removidos os 12 passos e três conclusões fabricadas; Application agora agrega jornada/etapas/tarefas obtidas pelo repositório Dapper existente.
- API e Web exigem autenticação/contexto e recusam tenant divergente; ausência de jornada retorna 404.
- Sem migration: reutilizado schema publicado. Detalhes em `docs/entregas/RC51-02I-ONBOARDING-CONTEXTO-PERSISTIDO.md`.
- BLOCKED: .NET, PowerShell, PostgreSQL 16, Docker e navegador ausentes; download do SDK bloqueado por HTTP 403.
- Próximo item: executar gates e isolamento runtime do onboarding em dois tenants; depois concluir convites.

---

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
# Correção de roteamento e shell Web — 2026-09-15

## Diagnóstico confirmado

| Arquivo | Problema observado | Impacto | Correção |
|---|---|---|---|
| `ExecutiveOperationsController.cs` e `GovernancaTransversalController.cs` | Duas actions `GET` publicavam `/QualidadeDados` e outras duas publicavam `/IntegracoesInternas`. | O matcher encontrava candidatos equivalentes e lançava `AmbiguousMatchException` antes da autorização/action. | A central transversal persistida tornou-se canônica em `/Governanca/QualidadeDados` e `/Governanca/IntegracoesInternas`; os caminhos legados são aliases GET explícitos no mesmo controller e redirecionam permanentemente. |
| `_Sidebar.cshtml` | Links apontavam para os padrões ambíguos. | Navegação autenticada reproduzia a falha. | Links agora apontam diretamente para as rotas canônicas, sem depender do alias. |
| `sigov-ui.js` | O clique de abertura/fechamento do drawer era registrado duas vezes e o drawer não continha/devolvia o foco. | Comportamento instável em telas estreitas e navegação incompleta por teclado. | Um único manipulador delegado controla o drawer; Escape, backdrop, contenção e devolução de foco compartilham o mesmo estado. |
| CSS do shell | A sidebar clara contrariava o token existente de navegação e larguras estavam repetidas em valores literais. | Contraste/contexto visual inconsistentes e manutenção de responsividade frágil. | Largura e altura de controle foram tokenizadas; sidebar usa azul-marinho e tabelas rolam apenas em contêiner próprio. |

Não foi hipótese: a duplicidade foi confirmada nos atributos `HttpGet`. A equivalência funcional foi decidida pela origem dos dados: `GovernancaTransversalController` usa `ITransversalGovernancaService`, autorização persistida, tenant e ocorrências reais; as actions removidas exibiam apenas o resumo executivo genérico.

## Evidência e limite do ambiente

- O teste existente `WebRuntimeSmokeTests` foi ampliado (sem nova classe) para inicializar o host real, auditar `EndpointDataSource`, exigir um único endpoint GET por padrão, conferir controller canônico/autorização e requisitar rotas canônicas e aliases como usuário anônimo.
- `dotnet` não está instalado neste container (`dotnet: command not found`); restore, build, compilação Razor e testes permanecem **BLOCKED** neste ambiente e a entrega não está declarada homologada.
- Não houve alteração de schema ou migration.

Próximo item exato do backlog: executar o gate Web com .NET SDK `10.0.100` e PostgreSQL 16, autenticar um usuário com e sem `governanca.qualidade.visualizar` e capturar as larguras 360, 390, 768, 1024, 1366 e 1920 px.

## Evolução de navegação e formulário — 2026-09-15

| Arquivo | Achado observado | Impacto | Correção aplicada |
|---|---|---|---|
| `_Sidebar.cshtml` | Todos os grupos renderizados formavam uma única lista extensa; não havia seleção de contexto funcional. | Alto custo de varredura e navegação especialmente ruim no drawer móvel. | O próprio conjunto de grupos já autorizado/renderizado alimenta um seletor de módulo; apenas o módulo corrente fica visível e a busca temporariamente percorre todos os destinos disponíveis. |
| `sigov-ui.js` | Estado compacto era persistido globalmente e não existia estado de módulo. | Preferência podia atravessar contextos, além de não haver segunda camada contextual. | Seleção de módulo é persistida somente na chave composta usuário/tenant/entidade/exercício; rota ativa vence preferência antiga e módulos removidos são descartados. |
| `_Navbar.cshtml` | Ausência de claims de entidade/exercício era apresentada como entidade “padrão” e ano do relógio. | A interface afirmava um contexto operacional não selecionado. | O cabeçalho agora informa explicitamente contexto/entidade/exercício não selecionados, sem fabricar contexto. |
| `Requisicoes/Nova.cshtml` e editor JavaScript | Formulário real não tinha resumo focável, indicação/ajuda acessível ou antiforgery disponível ao cliente; erros de API não recebiam foco. | Erros eram difíceis de localizar por teclado/leitor de tela, embora o serviço já preservasse idempotência e versão. | Campos receberam labels/ajuda/limites, agrupamento semântico, resumo, foco de erro, token antiforgery e bloqueio de envio preservado; rascunho continua reaproveitando os valores persistidos. |

Hipótese não promovida a correção: o catálogo histórico da sidebar contém grupos legados cuja contratação precisa ser comprovada em runtime; esta alteração não transforma visibilidade em autorização e não altera o avaliador persistente. Não houve migration. `node --check`, `git diff --check` e varreduras estáticas passaram. Restore/build/Razor/testes e capturas continuam **BLOCKED** porque o container não dispõe do SDK .NET 10 nem de host Web executável.

Próximo item exato: executar `WebRuntimeSmokeTests` e a jornada autenticada em PostgreSQL 16; validar o seletor e o formulário em 360/390/768/1024/1366/1920 px e zoom 200%, então confrontar cada grupo legado renderizado com a decisão do avaliador canônico antes de promover a navegação.
# Programação operacional de ordens — 2026-09-15

## Retomada comprovada

- Branch inicial `work`, HEAD `c237e39ae5fc44c70dc4b754bde1aca9bae3d3ba`; árvore limpa. Nenhum remoto ou upstream estava configurado no checkout.
- `global.json` exige SDK 10.0.100 com `latestFeature`; projetos configuram `net10.0`/C# 14, ASP.NET Core MVC/Razor, Dapper e Npgsql.
- Disponíveis: Git, Node 20 e npm. Ausentes: `dotnet`, `psql`, Docker/Podman e navegador Chromium/Playwright. Build, Razor, suites, PostgreSQL 16, Swagger/rotas runtime e capturas ficaram `BLOCKED`, sem declaração de homologação.

## Revisão e classificação

| Jornada | Estado nesta execução | Evidência/limite |
|---|---|---|
| Contratação/suspensão, usuários, perfis e sessões | implementada sem validação | Serviços/ADRs persistidos encontrados; runtime de suspensão, revogação e dois tenants bloqueado. |
| Ordens de serviço, programação e apontamentos | parcial | Contratos, Dapper, API e Razor reais existem; esta fatia corrigiu programação/reprogramação, mas o gate runtime segue bloqueado. |
| Inspeções e não conformidades | parcial | Há jornadas específicas por módulo, sem prova integrada com o encerramento desta OS. |
| Estoque | parcial | Consumo/devolução canônicos existem, sem prova PostgreSQL 16 nesta execução. |
| Minha Central | implementada sem validação | Consulta persistida e falha explícita existentes; ainda não incorpora a lista operacional de OS. |
| Layout e navegação | implementada sem validação | MVC/Razor/Bootstrap preservados; programação ganhou lista responsiva e formulário acessível, sem captura por falta de runtime. |

## Regras confirmadas e implementadas

- Programação aceita `ABERTA`, `EM_TRIAGEM`, `AGENDADA`, esperas e `REABERTA`; `CONCLUIDA`/`CANCELADA` não são reabertas pela agenda. Datas realizadas permanecem intocadas.
- Intervalos são semiabertos (`inicio < outroFim && fim > outroInicio`): término igual ao próximo início não conflita.
- Reprogramação exige motivo, preserva o agendamento anterior por inativação auditável e grava o novo registro na mesma transação da versão da ordem.
- Responsável é selecionado pelo cadastro canônico `os_tecnico`, dentro do tenant e não inativo. Atribuição não concede permissão; API mantém as policies `os.ordens.agendar`/`visualizar`.
- O lock da ordem, a revalidação de versão, a consulta de sobreposição e a gravação ocorrem na mesma transação. Sobreposição de responsável é revisão autorizável, não uma afirmação de indisponibilidade; a ausência de calendário não confirma disponibilidade.
- A tela oferece lista acessível e formulário por teclado, filtros de período/responsável/local/prioridade, estados de carregamento/vazio/erro e confirmação. Não foi introduzido drag-and-drop nem calendário como fonte de estado.

## Decisões pendentes e próximo item

Não existe no modelo legado desta OS vínculo canônico de ativo/recurso exclusivo, entidade/exercício, calendário institucional, feriados ou política persistida que torne toda sobreposição de pessoa bloqueante. Esses dados não foram inventados. A atribuição em lote também não foi criada sem contrato canônico de atomicidade. Indicadores existentes ainda usam `prazo_sla`; não foram renomeados nem apresentados como novos indicadores operacionais.

Próximo item exato: criar uma RC própria, após Gate A, para definir no banco a política de disponibilidade/sobreposição e o vínculo multi-entidade/recurso exclusivo da OS legada; então integrar Minhas Atividades e encerramento com inspeções/não conformidades/reservas canônicas e validar dois tenants em PostgreSQL 16. GED permanece por último.

---

Data: 2026-09-15. Minha Central e distribuição — filtros pessoais persistidos. Estado: **IMPLEMENTADA SEM VALIDAÇÃO RUNTIME / BLOCKED**.

- Estado inicial: branch `work`, HEAD `f7a7e199784983c3c0d5bcb47526db7c36cfca1b`, árvore limpa, sem remoto/upstream. SDK normativo `10.0.100`; Git, Node e `jq` disponíveis; `dotnet`, PostgreSQL/`psql`, Docker, PowerShell, `yq` e `shellcheck` ausentes.
- Minha Central ganhou filtro de estado permitido, contagem coerente com o conjunto filtrado, paginação SQL e ordenação determinística. Itens mostram módulo e identificação persistidos; ausência de prazo continua sem classificar atraso.
- Central e lista de distribuição agora salvam, atualizam por nome, renomeiam, excluem e definem filtro padrão no repositório canônico `usuario_preferencia`, sob chave versionada, tenant, usuário e tela. Parâmetros são validados por listas fechadas; filtro não concede autorização.
- Distribuição ganhou cabeçalho contextual, barra de filtro, estado vazio e paginação que preserva o estado. A autorização canônica do controller continua sendo reavaliada antes da consulta.
- Sem migration: foi reutilizada a persistência canônica existente. GED não foi alterado.
- Validação estática: `git diff --check`, busca de conflitos e sintaxe JavaScript passaram. **BLOCKED:** restore/build/Razor/testes/Swagger, PostgreSQL 16, dois tenants, sessão/revogação, navegador e capturas nas seis larguras, pois os runtimes não existem no host.
- Próximo item exato: executar Gate A no SDK 10.0.100/PostgreSQL 16 e provar os filtros com dois tenants, revogação e reload; depois substituir a projeção genérica `pendencia_operacional` por adaptadores de leitura das seis fontes canônicas, com falha parcial explícita por fonte.

## Incremento da jornada de processos — 2026-09-22

A auditoria dirigida da jornada canônica `Processos` identificou falta de atomicidade e concorrência otimista em movimentação/conclusão/cancelamento, filtros de entidade/exercício/período ignorados e renderização dinâmica sem codificação. O incremento independente corrige esses pontos, registra encerramento/cancelamento na timeline e revisa a ajuda/ações do detalhe. Build e integração continuam bloqueados pela ausência de .NET 10, PostgreSQL, connection string e aplicação autenticada. Evidências, classificação conservadora e pendências estão em `docs/execucao/AUDITORIA_JORNADA_PROCESSOS_2026-09-22.md`.
