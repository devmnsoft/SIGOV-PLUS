# Relatório Ciclo 2 · Sprint 5 — Fechamento Operacional de Educação: FUNDEB/custos/rateio + avaliação do magistério + maturidade de indicadores/Educacenso

Data: 2026-10-13 · Trilha: commit `bcb6d92d` (Ciclo 2 S4) + commit desta sprint (código/migration/suítes/driver/evidências) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

O FUNDEB do SIGOV PLUS sai de parâmetro/receita/aplicação/centro (Ciclo 1 S9) para **competência fiscal fechada e auditável, conformidade medida contra o mínimo do banco, rateio de custos com soma exata e estorno comandado**, junto da **avaliação do magistério com fechamento e consolidado imutável** e da **maturidade do Educacenso** (decisão documentada das inconsistências, encerramento só com ALTA zerada e eventos de sincronização registrados no painel) — BR-5.1 a BR-5.7 do roadmap `docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md`. Base normativa da fatia: **LC 114/2021** (FUNDEB — mínimo de aplicação em remuneração básica de profissionais da educação em carreira, default 70%, parametrizável por esfera/tenant), **Lei 11.494/2007** e o **Censo Escolar/INEP** (janela de coleta, tratamento documentado de inconsistências); e as regras 4/6/7/11/13/24 do AGENTS.md (PK bigint identity, migration idempotente com consolidados sincronizados, banco como autoridade dos parâmetros, falha nomeada explícita, nada municipal hardcoded — tudo multi-esfera pela entidade do comando autenticado).

Migration idempotente `database/postgres/migrations/20261013120000_evo_c2s5_fundeb_rateio_magisterio_educacenso.sql` (SHA256 `9a31c324…`), **aplicada 2× no PG16 sem erro** (`on conflict do nothing` + `if not exists` em tudo; pós-condição `C2S5-POSTCOND` que falha explicitamente se faltar qualquer artefato — regra 13):

- **nova tabela** `sigov.educacao_fundeb_competencia` (PK bigint identity — regra 4): tenant × entidade × `competencia date`, `status` FECHADA|REABERTA, `justificativa`, `reaberta_em`, auditoria jsonb + backstop `ux_fundeb_competencia_tenant_date` (uma linha vigente **FECHADA** por tenant×data; reabertura libera a posição);
- `educacao_fundeb_aplicacao`: dedupe legado preservando `min(id)` + backstop `ux_fundeb_aplicacao_tenant_comp_centro` sobre `(tenant_id, coalesce(competencia,'2999-12-31'), coalesce(centro_custo_id,0))` — competência nula agrupa na **sentinela 2999-12-31** (caminho legado sem competência permanece permitido, mas único por centro); estados terminais liberam;
- `educacao_custo_rateio`: dedupe legado (`ESTORNADO` já é terminal) + backstop `ux_custo_rateio_tenant_origem_criterio_comp` sobre `(tenant_id, coalesce(centro_custo_id,0), coalesce(tipo,''), coalesce(competencia,'2999-12-31'))` restrito a `tipo in ('MATRICULAS','RECEITA','PESO_MANUAL')` — um rateio vigente por origem×critério×competência; `ESTORNADO` sai da soma e libera a posição;
- `educacao_avaliacao_resultado`: dedupe legado + backstop `ux_avaliacao_resultado_tenant_avaliacao_servidor` sobre `(tenant_id, coalesce(referencia_id,0), coalesce(servidor_id,0))` — um resultado vigente por avaliação×servidor (estornos liberam para retificação);
- seed `parametro_modulo` canônico `FDEB_CONFORMIDADE_MINIMO_PCT`=70 (INTEGER, MODULO='EDUCACAO', ordem 210) — mínimo de conformidade do módulo com override por tenant em `parametro_modulo_valor`; a precedência de leitura é: parâmetro FUNDEB do próprio tenant (`TAXA_MINIMA_APLICACAO` em `sigov.educacao_fundeb_parametro`) → parâmetro do módulo → falha nomeada (regras 11/13);
- manifest **232 entradas** + os 6 arquivos consolidados sincronizados byte a byte (regra 7); `SaasComercialTests.cs` atualizado para 232 entradas no padrão existente (split 233).

Regras de negócio implementadas (camada `EducacaoAvancadaRepository` + contratos + controller/filtro):

- **BR-5.1 — Parâmetro FUNDEB com vigência anual:** `POST fundeb/parametros/lancar` ganha `ano` (faixa canônica 2000–2100, 400 fora dela) persistido em `ano_letivo`; um código vigente por tenant continua valendo (409 `FUNDEB_PARAMETRO_CODIGO_EXISTENTE` + backstop S9).
- **BR-5.2 — Competência fechada:** `POST fundeb/competencias/fechar` {competencia,justificativa?} — criação ou re-fechamento idempotente (linha REABERTA → FECHADA; 23505 → rollback + select devolvendo o id original); `POST fundeb/competencias/reabrir` exige justificativa (400 `COMPETENCIA_REABERTURA_JUSTIFICATIVA_OBRIGATORIA`) e só parte de FECHADA (senão 409 `COMPETENCIA_FUNDEB_NAO_FECHADA`), gravando `reaberta_em` + auditoria. Enquanto FECHADA, receita, aplicação, lançamento de custo e rateio nessa data → **409 `COMPETENCIA_FUNDEB_FECHADA`** nomeada (helper `CompetenciaFundebFechadaAsync` retrofitado nos lançamentos). Aplicação única por competência×centro: segunda vigente → 409 `FUNDEB_APLICACAO_DUPLICADA` + backstop (sentinela para legado sem competência).
- **BR-5.3 — Painel de conformidade:** `GET fundeb/conformidade?unidadeEscolaId=&competencia=` (DTO `EducacaoFundebConformidadeDto`): `percentual = Round(aplicações MAGISTERIO ÷ total × 100, 2, AwayFromZero)` vs mínimo lido do banco (precedência acima; prova ao vivo: parâmetro tenant `TAXA_MINIMA_APLICACAO=60` prevaleceu sobre o 70 do módulo — regra 11); **zero aplicações ≠ indisponível** → `percentual=0` com `nota` explícita ("sem aplicacoes no recorte") e `emConformidade=false`; recorte por unidade escolar (escola da aplicação OU do centro) e por competência; `ultimoLancamento` auditável; sem mínimo no banco → falha nomeada (regra 13).
- **BR-5.4 — Rateio com soma exata e estorno comandado:** `POST custos/lancamentos/lancar` {centroCustoId,competencia,valor} fecha o resíduo da S10 (valor do rateio auditável por origem×competência; valor > 0; competência fechada bloqueia). `POST custos/rateios/lancar` {centroCustoIdOrigem,competencia,criterio,pesosManual?}: critério canônico MATRICULAS|RECEITA|PESO_MANUAL (400 `RATEIO_CRITERIO_INVALIDO`); total = soma dos lançamentos ATIVA da origem na competência (vazio/não positivo → 400 `RATEIO_TOTAL_INVALIDO`); pesos por destino — MATRICULAS = matrículas ATIVA/CONFIRMADA por escola do centro destino, RECEITA = soma das aplicações FUNDEB do destino, PESO_MANUAL = mapa informado (destinos vigentes do escopo, peso > 0 — 400 `RATEIO_PESOS_INVALIDOS`); valores `Round(total×peso/soma, 2)` com **residual absorvido no maior peso** (tie → menor id) — soma dos derivados == total **exata**; cabecalho `educacao_custo_rateio` (tipo=critério) + lançamentos `RATEIO_DERIVADO` (referencia_id=rateio) em transação única; idempotência origem×critério×competência (409 `RATEIO_DUPLICADO` + backstop; ESTORNADO libera). `POST custos/rateios/{id}/estornar` {justificativa obrigatória — 400 `RATEIO_ESTORNO_JUSTIFICATIVA_OBRIGATORIA`} — cabeçalho `ESTORNADO` + derivados `CANCELADA` com `justificativa = 'ESTORNO_RATEIO:<id>'` (auditoria derivada→origem), idempotente; rateio inexistente/inativo → 404 `RATEIO_CUSTO_INEXISTENTE`.
- **BR-5.5 — Avaliação do magistério:** `POST avaliacao-magisterio/lancar` {nome,periodoInicio,periodoFim,instrumento obrigatório — 400 `AVALIACAO_INSTRUMENTO_OBRIGATORIA`; fim ≥ início — 400 `AVALIACAO_PERIODO_INVALIDO`; unidadeId? (escopo institucional validado — multi-esfera), ano?} nasce ABERTA. `POST …/{id}/resultados/lancar` {servidorId,nota}: nota em [0,10] (400 `AVALIACAO_NOTA_INVALIDO`), servidor ativo do tenant validado em `sigov.servidor` (404 `AVALIACAO_SERVIDOR_INEXISTENTE`), **um resultado vigente por servidor×avaliação** (409 `AVALIACAO_RESULTADO_EXISTENTE` + backstop; segue valendo após reabertura). Ciclo ABERTA→FECHADA→REABERTA: `fechar` idempotente grava **consolidado imutável** em `dados->consolidado` (média round 2, servidoresAvaliados, participacaoPct = avaliados distintos ÷ servidores ativos do escopo); FECHADA trava resultados (409 `AVALIACAO_FECHADA_RESULTADOS_BLOQUEADOS`); `reabrir` só de FECHADA com justificativa (400 `AVALIACAO_REABERTURA_JUSTIFICATIVA_OBRIGATORIA`; de ABERTA → 409 `AVALIACAO_ESTADO_INVALIDO`), aceitando retificação e re-fechamento com recálculo (prova ao vivo: média 7.00 com 3 avaliados → 8ª nota → 7.25 com 4).
- **BR-5.6 — Educacenso maduro:** decisão única e documentada por inconsistência — desfecho canônico CORRIGIDA | ACEITE_COM_MOTIVO (motivo obrigatório — 400 `EDUCACENSO_DESFECHO_INVALIDO` / `EDUCACENSO_MOTIVO_OBRIGATORIO`; status final CORRIGIDA/ACEITA; decidir de novo → 409 `EDUCACENSO_INCONSISTENCIA_JA_DECIDIDA`); `POST educacenso/lotes/{id}/encerrar` só encerra com **ABERTAS zeradas** — ALTA aberta tem bloqueio próprio e nomeado (409 `EDUCACENSO_LOTE_ALTA_ABERTA`), demais ABERTAS → 409 `EDUCACENSO_LOTE_INCONSISTENCIAS_ABERTAS`; encerrado é terminal (409 `EDUCACENSO_LOTE_ESTADO_INVALIDO`) e grava evento `ENCERRAMENTO_LOTE`; `POST educacenso/eventos/sincronizar` {detalhe?} registra evento SINCRONIZACAO e o painel `GET educacenso/dashboard` agora exibe **`ultimaSincronizacao`** (máximo do rastro — null explícita enquanto nunca sincronizado).
- **BR-5.7 — Projeção de matrícula vigente:** revalidada ao vivo nesta sprint (`projecoes-matricula/lancar`, uma vigente por escola×ano — 409 `PROJECAO_MATRICULA_EXISTENTE` + backstop S10); o custo/aluno projetado que a consome entra na S20 (decisão registrada: o campo não é exposto no DTO desta fatia).

Camada API: 14 endpoints novos em `api/educacao` (controller de indicadores: `fundeb/parametros/lancar` (ganhou ano), `fundeb/competencias/fechar|reabrir`, `fundeb/conformidade` (GET), `custos/lancamentos/lancar`, `custos/rateios/lancar`, `custos/rateios/{id}/estornar`, `avaliacao-magisterio/lancar`, `avaliacao-magisterio/{id}/resultados/lancar|fechar|reabrir`, `educacenso/inconsistencias/{id}/decidir`, `educacenso/lotes/{id}/encerrar`, `educacenso/eventos/sincronizar`); **+26 exceções nomeadas novas** (total da família Educação) e **27 mapeamentos novos** (400/404/409) no `EducacaoFalhasNomeadasFiltro` (regra 13 — nenhum 500 genérico para falha de domínio).

## Suítes (estendidas, sem novas classes — regra 14)

- `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs`: fact existente estendido com asserts de fonte da S5 (conteúdo da migration: a nova tabela, os 4 backstops nomeados, dedupes, parâmetro canônico e POSTCOND; manifest contém a entrada S5; as 14 rotas tipadas presentes no controller; mapeamentos-chave do filtro; tokens `COMPETENCIA_FUNDEB_FECHADA`/`RATEIO_DUPLICADO`/`EDUCACENSO_LOTE_ALTA_ABERTA` nas exceções; `ESTORNO_RATEIO:`/`'RATEIO_DERIVADO'`/consolidado jsonb no repositório) — além de manter a asserção do fact legado S10 alinhada ao novo campo `UltimaSincronizacao` do dashboard.
- `tests/Sigov.IntegrationTests/SaasComercialTests.cs`: contagem de manifest atualizada (232 entradas / split 233) no padrão existente.
- Executado após as últimas correções C#: **850 verdes (Unit 522 / Api 158 / Integration 170, 0 ignorados)**; build Release `-warnaserror` de `Sigov.sln` 0w/0e a cada rodada com mudança de código + revalidação integral no fechamento.

## Matriz verificável da corrida oficial (bloco E19 no driver Tier-2 — 35 checks)

Fixture própria e isolada (marcadores `c1e26-c2s5-%`/`C2S5-%`, dados fictícios — regra 10): escola + ano letivo 2026 + curso/série/turma + 3 alunos matriculados ATIVA (peso MATRICULAS conhecido: 3) + 4 servidores em `sigov.servidor` + 4 centros de custo via SQL (MAGISTERIO vinculado à escola, MATERIAL, sem categoria, origem INFRAESTRUTURA) + reset determinístico do estado fundeb/custo/avaliacao/educacenso/projeção do tenant (bloco final da suíte; CX re-limpa ao fim — `restantes=0`).

| Checks (E19) | Cenário | Esperado |
|---|---|---|
| fixture.c2s5-pronto | reset + criações idempotentes | todos os ids > 0 (escola/turma/3 matrículas/4 servidores/4 centros) |
| parametro.vigencia-anual-200 | POST parametros/lancar {TAXA_MINIMA_APLICACAO,60,2026} | 200; banco `ano_letivo\|valor` = `2026\|60.00` |
| parametro.duplicado-vigente-409 / parametro.ano-invalido-400 | 2º parâmetro mesmo código; ano 1999 | **409 `FUNDEB_PARAMETRO_CODIGO_EXISTENTE`**; **400** (faixa 2000–2100) |
| aplicacao.sem-competencia-legado-200 / aplicacao.duplicada-mesmo-centro-409 | 2× mesma aplicação sem competência (MAG 4000) | 200; **409 `FUNDEB_APLICACAO_DUPLICADA`** (bucket sentinela 2999-12-31) |
| receita.competencia-aberta-200-duplicada-409 / aplicacao.competencia-200-duplicada-409 | FPE@hoje 10000 + dup; OUTRO@hoje 4000 + dup | 200/409 `FUNDEB_RECEITA_DUPLICADA`; 200/409 `FUNDEB_APLICACAO_DUPLICADA` (braço com competência) |
| competencia.fechar-idempotente-200 | fechar hoje + repetir | 200 ambos, **mesmo id** (rastro em `educacao_fundeb_competencia`) |
| lancamentos.competencia-fechada-409 | receita e aplicação novas na fechada | **409 `COMPETENCIA_FUNDEB_FECHADA`** nos dois |
| competencia.reabrir-justificado-libera-200 | reabrir sem/justificativa/já reaberta + nova aplicação | **400 `COMPETENCIA_REABERTURA_JUSTIFICATIVA_OBRIGATORIA`**; 200; **409 `COMPETENCIA_FUNDEB_NAO_FECHADA`**; 200 (mag agora 8000) |
| conformidade.magisterio-vs-minimo-banco-200 | GET conformidade (sem filtro) | 200; aplicacoesMagisterio=8000, total=12000, receitas=10000, **percentual=66.67, minimoPct=60** (parâmetro TENANT prevalece sobre o 70 do módulo — regra 11), emConformidade=true (LC 114/2021) |
| conformidade.recorte-vazio-zero-por-cento-200 | GET ?competencia=2026-01-01 | 200; percentual=0 com `nota` "sem aplicacoes…" (zero ≠ indisponível), emConformidade=false |
| conformidade.recorte-unidade-escolar-200 | GET ?unidadeEscolaId=escola C2S5 | total=8000 (só centros da escola), pct=100, conforme=true |
| lancamento.custo-tipado-200-valor-invalido-400 | custos/lancamentos/lancar 100.04 origem@hoje; valor -50 | 200 (fecha resíduo S10); **400** |
| rateio.peso-manual-soma-exata-200 | rateio PESO_MANUAL 1:1:1 | 200; banco `3\|100.04\|ATIVA\|PESO_MANUAL\|100.04` — 3 derivados somam **exatamente** o total (residual no maior peso), cabeçalho atômico |
| rateio.duplicado-criterio-total-4xx | repetido; criterio OUTRA; sem lançamentos @2026-01-01 | **409 `RATEIO_DUPLICADO`**; **400 `RATEIO_CRITERIO_INVALIDO`**; **400 `RATEIO_TOTAL_INVALIDO`** |
| rateio.matriculas-peso-por-escola-200 | critério MATRICULAS | 1 derivada no único centro com escola matriculada = 100.04 (banco `1\|100.04\|<mag>`) |
| rateio.receita-aplicacoes-fundeb-200 | critério RECEITA | 2 derivados (mag 8000 × outro 4000; terceiro sem aplicação fora) somando 100.04 |
| rateio.estorno-comandado-idempotente-200 | estornar sem just / com / repetir | **400 `RATEIO_ESTORNO_JUSTIFICATIVA_OBRIGATORIA`**; cabeçalho `ESTORNADO` + 3 derivados `CANCELADA` com `ESTORNO_RATEIO:<id>`; 200 idempotente |
| rateio.estorno-libera-unicidade-200 / rateio.inexistente-404 | relançar MESMO origem×critério×comp; id 9999999999 | 200 novo id (ESTORNADO sai do índice); **404 `RATEIO_CUSTO_INEXISTENTE`** |
| rateio.competencia-fechada-bloqueia-409 | re-fechar hoje: lançamento + rateio; reabrir | **409 `COMPETENCIA_FUNDEB_FECHADA`** nos dois; 200 |
| avaliacao.lancar-instrumento-obrigatorio-200 | sem instrumento; período invertido; válida | **400 `AVALIACAO_INSTRUMENTO_OBRIGATORIA`**; **400 `AVALIACAO_PERIODO_INVALIDO`**; 200 |
| avaliacao.resultado-validacao-servidor-200 | nota 11; servidor 9999999999; sv1=9; sv1 de novo | **400 `AVALIACAO_NOTA_INVALIDO`**; **404 `AVALIACAO_SERVIDOR_INEXISTENTE`**; 200; **409 `AVALIACAO_RESULTADO_EXISTENTE`** |
| avaliacao.fechamento-consolidado-imutavel-200 | sv2=7, sv3=5, fechar ×2 | 200; banco `FECHADA\|7.00\|3\|true` (média exata, consolidado imutável, participação em 0..100) |
| avaliacao.fechada-bloqueia-resultados-409 | resultado após fechar | **409 `AVALIACAO_FECHADA_RESULTADOS_BLOQUEADOS`** |
| avaliacao.reabertura-retificacao-200 | reabrir sem/com just; ja avaliado; sv4=8; re-fechar | 400 nomeada; 200; 409 (unicidade vigente persiste); 200; consolidado recalculado `FECHADA\|7.25\|4` |
| avaliacao.estado-invalido-reabrir-aberta-409 | reabrir avaliação ABERTA | **409 `AVALIACAO_ESTADO_INVALIDO`** (ciclo ABERTA→FECHADA→REABERTA) |
| educacenso.lote-ano-unico-200 | lote 2026 + dup + 3 inconsistências | 200; **409 `LOTE_EDUCACENSO_ANO_EXISTENTE`**; ALTA/MEDIA/BAIXA 200 |
| educacenso.encerramento-alta-aberta-409 | encerrar; desfecho OUTRA; ACEITE sem motivo | **409 `EDUCACENSO_LOTE_ALTA_ABERTA`**; **400 `EDUCACENSO_DESFECHO_INVALIDO`**; **400 `EDUCACENSO_MOTIVO_OBRIGATORIO`** |
| educacenso.decisao-documentada-200 | CORRIGIDA + redecidir; encerrar c/ abertas; MEDIA+BAIXA | 200; **409 `EDUCACENSO_INCONSISTENCIA_JA_DECIDIDA`**; **409 `EDUCACENSO_LOTE_INCONSISTENCIAS_ABERTAS`**; 200/200 |
| educacenso.lote-encerrado-consistente-200 | encerrar de novo | 200; banco `ENCERRADA\|1 evento\|CORRIGIDA:CORRIGIDA,ACEITA:ACEITE_COM_MOTIVO,CORRIGIDA:CORRIGIDA`; **409 `EDUCACENSO_LOTE_ESTADO_INVALIDO`** |
| educacenso.sincronizacao-registrada-200 | eventos/sincronizar + dashboard | 200; `ultimaSincronizacao` do painel non-null |
| projecao.matricula-unica-escola-ano-200 | projeção 120 @2026 + dup | 200; **409 `PROJECAO_MATRICULA_EXISTENTE`** (BR-5.7; alimenta S20) |

**Corrida oficial: 306/306 (271 baseline das Sprints anteriores + 35 E19), `DRIVER_EXIT=0`, cleanup `restantes=0`.** Rodadas triadas sem máscara (logs preservados em `artifacts/c1_tier2_e2e/s5_e2e_round{1,2,3}.log` + consoles `s5_api_round{1,2,3}.log`): rodada 1 = 294/12, rodada 2 = 304/2.

## Defeitos corrigidos NA PRÓPRIA prova (sem máscara)

C# (detectados pela rodada 2 e corrigidos antes da corrida oficial):

1. **`LancarCustoLancamentoAsync` sem `CommitAsync`** — transação aberta em `await using` rolava o lançamento de custo inteiro; commit explícito adicionado.
2. **`EducacaoAvaliacaoReabrirRequest(string Justificativa)` obrigatório no binding** — `POST …/reabrir {}` caía em validação de framework (400 genérico problem+json) antes de chegar à falha nomeada; contrato virou `string? Justificativa=null` e o repositório continua exigindo a justificativa com `AVALIACAO_REABERTURA_JUSTIFICATIVA_OBRIGATORIA` (regra 13 — validação do domínio é a autoridade, padrão já usado no estorno de rateio).
3. **Materialização Dapper** — `(await q).ToList().ConfigureAwait(false)` (`ConfigureAwait` pertence à `Task`) e narrowing CS8602 em propriedade nullable resolvido com local; build `-warnaserror` 0w/0e a cada rodada.

Driver/harness (rodadas 1–2):

4. Envelope `ApiResponse<T>`: DTO de GET lido na raiz do corpo em vez de `.data` (conformidade ×3 + dashboard educacenso vieram nulos);
5. `pesosManual` com chaves numéricas **sem aspas** (`{92:1}`) = JSON inválido → 400 de model-binding em todo rateio PESO_MANUAL (as chaves viraram strings — binding `Dictionary<long,decimal>` do System.Text.Json);
6. Booleano do psql imprime `true` (não `t`) e `numeric(18,2)` imprime `60.00` (não `60`) nas comparações por texto;
7. Token grafado errado na asserção: `EDUCACENSO_JA_DECIDIDA` contra o real `EDUCACENSO_INCONSISTENCIA_JA_DECIDIDA` (confirmado na fonte, L588 de `EducacaoEscopoExceptions.cs`).

Suítes: o fact legado S10 assercia a assinatura exata do record do dashboard — atualizado para o novo campo `UltimaSincronizacao` (manutenção de expectativa, sem nova classe — regra 14).

## Notas de ambiente e resíduos declarados (regra 13 — nada mascarado)

- Memória livre crônica baixa neste host: entre a rodada 1 e a 2 os apps locais morreram (`env.*-up` = 000) — corrida oficial relançada com kill dirigido (`Stop-Process dotnet`) + shells dedicados + sonda de readiness (política desde a Sprint 8).
- Custo/aluno **projetado** (consumo da projeção BR-5.7) fica na S20 — decisão registrada; o DTO de conformidade desta fatia não expõe o campo.
- Escopo da participação da avaliação = servidores ativos do tenant (`sigov.servidor` template wide) — recorte por unidade é exposto como `unidadeId` na avaliação, não no denominador (simplificação declarada).
- `validate-script-completop` (seed `admin@sigov.local`), `rc50_25` órfã e `p1_dev.schema_migrations` sem as entradas 20261012xxxx permanecem como declarado nas sprints anteriores.

## Jornada da fatia e gates

- **Jornada da fatia: IMPLEMENTADO + VALIDADO POR TESTES + EXECUTADO EM INTEGRAÇÃO + HOMOLOGADO PELA INTERFACE.**
- Gates: migration aplicada 2× (idempotência) + POSTCOND; manifest 232 + 6 consolidados `-Verify` OK (regras 6–7); build Release `-warnaserror` 0w/0e; suítes **850 verdes (Unit 522 / Api 158 / Integration 170)**; Tier-2 E2E HTTP **306/306** na corrida oficial com cleanup `restantes=0`; driver PS validado pelo parser (`SYNTAX_OK` — regra 19); RC própria desta sprint (regra 20 — P1, fora da RC50.68A).
- Próximo exato: **Ciclo 2 Sprint 6 — Jornadas pelas telas reais (substituir a fachada estática)** (BR-6.1 a BR-6.6 do roadmap). GED por último.
