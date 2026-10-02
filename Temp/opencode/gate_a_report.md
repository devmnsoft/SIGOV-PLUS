# RELATORIO DO GATE A - RC50.68A (aprovacao: quorum, causas, reavaliacao, idempotencia, concorrencia, historico)

Data: 2026-09-30 (UTC). Base: `c6407f6c93eb65b094cd043a2a82246e2ffbc614` (main, "melhorias"). Todo o trabalho segue **uncommitted** sobre essa base.

## VEREDICTO

**GATE A = PASS** (implementado + testado contra API e banco vivos). Homologacao oficial do conjunto acontece no Bloco D; nada aqui e declarado "pronto" para o SIGOV inteiro.

| Item | Status |
|------|--------|
| Build .NET 10 / C# 14 (sigov.tests.sln) | PASS (0 erros, estado final do codigo) |
| Testes oficiais | PASS (UnitTests 423 + ApiTests 132 + IntegrationTests 141 = **696/696**, 0 falhas) |
| Jornada S0-S10 (`gate_jornada.ps1`) | PASS (JOURNEY_FAILS=0, exit 0) |
| GATE A extra (`gate_a_extra.ps1`: SANITY + G1-G4 + SNAPSHOT) | PASS (exit 0, zero asserts em falha) |
| `scripts/check-migration-catalog.sh` | static=PASS (SQL=207 manifest=197 baseline=193 governed_orphans=10); a nota BLOCKED do proprio script refere-se a execucao via runner canonico (validacao semantica) — execucao/reexecucao idempotente da migration do Bloco A ja provada ao vivo no PG16 docker (aplicada 2x) |
| `scripts/check-api-route-conflicts.sh` | PASS ("Nenhum conflito direto em 634 rotas API", incluindo a rota nova `requisicoes/{id}/reavaliar-encaminhamento`) |
| Reset limpo (`gate_reset.ps1`) | PASS (ZUMBIS=0, PENDENCIAS_DEMO=0, idempotencia do tenant demo zerada, RESET_OK) |

## O QUE FOI IMPLEMENTADO (Bloco A, diff nao commitado)

### Migration (idempotente, aplicada e sincronizada)
- `database/postgres/migrations/20260930120000_compras_aprovacao_causa_bloqueio_e_resultado_idempotencia.sql` (checksum `f7a8d7b2...cc99b5`): `compras_empresarial_aprovacao.causa_bloqueio varchar(40)` e `compras_empresarial_idempotencia.resultado jsonb`.
- `manifest.json` + `script_completo.sql` + `script_completo_dev.sql` + `database/script_completo.sql` + `script_completop.sql` sincronizados (regra 7).

### Application — `ComprasContracts.cs`
- `AprovacaoQuorum.NivelAnteriorCoberto`: interpretacao centralizada do quorum por nivel canonico (nivel N so e decisivel com todos os niveis anteriores aprovados/cobertos).
- `ReavaliarEncaminhamentoRequest/Resultado` e `AprovacaoDecisaoResultado(..., bool Repetido)`.

### Infrastructure — `AprovacaoRequisicaoRepository.cs` / `ComprasRepositories.cs`
- Causas canonicas de bloqueio gravadas na etapa: `SEM_POLITICA` | `SEM_APROVADOR` | `ALCADA_INSUFICIENTE` (diferenciadas, sem ambiguidade).
- **Reavaliar encaminhamento** (nova operacao): espelha `EnviarAsync` — k = primeiro nivel com limite >= total; niveis > k vao a CANCELADO; clona o template para 1..k incluindo causa; regra X1 (solicitante excluido da designacao) + `.Take(3)`. Reavaliar sem mudanca de condicao nao duplica etapas nem pendencias (repetido=false apenas quando ha mutacao; desbloqueado=false senao).
- `ConcluirPendenciasAsync(params string[]? tipos)`: pendencia so fecha com causa comprovadamente resolvida; upsert `pendencia_operacional` sem duplicidade (ON CONFLICT ... WHERE status in ABERTA/EM_TRATAMENTO).
- **Idempotencia vinculada ao agregado** nas 4 operacoes (criar, enviar, decidir, reavaliar) + politica: replay de mesma chave + mesmo `request_hash` devolve o `resultado jsonb` original SEM mutacao; mesma chave + hash diferente => 409; falha de decisao nao grava idempotencia. Escrita canonica `'id',@pol` (numerico) + leitura tolerante no replay (linhas legadas com id como string).
- **Ordem consistente de bloqueios** (I1) em `DecidirAsync`; guard order: status -> version (409) -> aprovador null (422) -> designee -> segregacao -> elegibilidade -> quorum. Mensagens institucionais exatas preservadas.
- Advisory locks transacionais: DECISAO/REAVALIAR `{tenant:D}|APROVACAO|REQUISICAO|{id:D}`; ENVIO `{tenant:D}|REQUISICAO|{id:D}|ENVIO`.
- Espelho S1 em `governanca_ocorrencia_historico` + snapshot H1 com classificacao COMPLETO/PARCIAL/INDISPONIVEL; nome do aprovador via join `sigov.os_tecnico` (D7).
- Catches endurecidos: `try { await tx.RollbackAsync(ct); } catch (InvalidOperationException) { } throw;` (Npgsql 10 nao expoe IsComplete/IsCompleted; transacao ja finalizada dentro do bloco preserva a excecao original).

### Correcoes encontradas/confirmadas durante a execucao do gate (prova em `npgrepro`)
1. **UPSERT dos niveis da politica**: `UNIQUE (tenant_id, politica_id, ordem)` simples (nao parcial) fazia insercao pos-soft-delete colidir com linhas desativadas (500 na ampliacao/reducao). Corrigido com `on conflict (tenant_id,politica_id,ordem) do update set limite=@limite,is_deleted=false,...` (sem nova migration).
2. **500 persistente no UPDATE de reuso da politica — CAUSA RAIZ CONFIRMADA**: Dapper 2.1.35 **nao achata tipo anonimo aninhado**; `new { args, id = reuso.Value }` enviava `@nome` etc. como literal => `42601 syntax error at or near "=@"` (POSITION 61, exato). Repro minimo T1 (aninhado) falha identico ao API; T2 (anonimo achatado) passa. Fix: `new { args.t, args.nome, ..., id = reuso.Value }`. Unica ocorrencia do padrao no `src/` (verificado por grep).
3. **Semantica de eventos preservada (preexistente no HEAD)**: decisao APROVAR em nivel final emite `APROVADA` (nivel agregado); niveis intermediarios emitem `ETAPA_APROVADA`. Sem pino de teste; a assercao do gate extra foi alinhada a essa semantica com rigor equivalente (exatamente 1 evento por decisao — replay e perdedor da corrida nao duplicam).
4. Instrumentacao temporaria `[SIGOV-DBG-500]` no controller foi usada para diagnosticar e **removida** (codigo restaurado; build final sem a linha).

### Api — `ComprasEmpresariaisController.cs`
- Nova rota `[HttpPost("requisicoes/{id:guid}/reavaliar-encaminhamento")]` com policy de autorizacao e antiforgery/contratos iguais as demais. Mapa `Falha(ex)` mantido (409/404/401/400/422/500).

### Tests (sem novas classes — regras 14)
- 4 pins de `PostBuild01RegressionTests` ATUALIZADOS apos matriz que confirmou reestruturacao intencional do commit "melhorias" (regra preservada/ampliada); X1 mantido como comportamento intencional.

## CENARIOS DO GATE EXTRA (todos PASS na rodada final)

- **G1** Rq6 (500000 sob 50000/250000): `ALCADA_INSUFICIENTE` => ampliacao da politica (50000/600000) => `SEM_APROVADOR` + reavaliacao #1 sem mutacao + replay idempotente.
- **G2** Rq2: politica INATIVA nao libera; reativacao + reavaliar libera; aprovacao conclui.
- **G3** Rq7 (600000 sob 50000/600000): `SEM_APROVADOR` => politica reduzida a 1 nivel (700000) => n2 CANCELADA fora da cobertura (prova do fix L282: `maximoNivel` ignora etapas CANCELADAS) => APROVAR n1 conclui a requisicao com 2 etapas (1 APROVADA + 1 CANCELADA, sem duplicacao).
- **G4** Rq5: quorum por nivel anterior (422 com mensagem institucional exata); corrida real sobre a mesma etapa (200 vencedor + 422 "ja foi decidida" perdedor); replay do vencedor `repetido=true` reexpondo o original; chave do vencedor persistiu `resultado` (perdedor 422 nao persiste); eventos exatos (1 `ETAPA_APROVADA` + 1 `APROVADA`).
- **SNAPSHOT final**: 5 APROVADA (Rq1,Rq2,Rq3,Rq5,Rq7) + 1 REJEITADA (Rq4) + 1 PENDENTE (Rq6); exatamente 1 pendencia aberta (Rq6 `SEM_APROVADOR`).

## EVIDENCIAS

- `jornada_evidence.txt` (append acumulado; rodada final GATE A EXTRA a partir da linha 521 ate 959: corrida 200/422 na linha 824, n2 aprovada na 838, snapshot final na 857).
- Suítes: execucao fresca apos o estado final do codigo (696/696).
- Imagem API: `docker compose build api` com o binario final (contem upsert + params achatados; verificado por conteudo UTF-16 no DLL).

## LIMITACOES DOCUMENTADAS (nao sao defeitos; nao bloqueiam o GATE A)

- Nenhum usuario do tenant demo tem alçada >= 600000 (101=50000, 102=250000): requisicoes acima do topo da alçada terminam em `PENDENTE_APROVACAO`/`SEM_APROVADOR` propositalmente (Rq6 encerra assim o gate).
- `db-migrations` sai com exit 3 (lacuna preexistente de ledger anterior a esta RC): o container da API e iniciado com `docker start sigov-api`; comportamento documentado e nao mascarado (mesma documentacao do gate completo anterior).
- Host local tem postgres nativo antigo na porta 5432 do Windows: validacao SQL feita sempre via `docker exec sigov-postgres psql`.

## FORA DO RECORTE DO GATE A (vai para Blocos B/C/D)

- Telas reais de Cotações/Nova Cotação/Detalhe/Comparativo/Pedidos (Bloco B).
- Contexto/UX: cabecalho organizacao/unidade/exercicio/modo, menu por operacoes autorizadas, ajuda, tema claro/escuro, breakpoints (Bloco C).
- Validacao completa de instalacao limpa/upgrade/reexecucao do consolidado + veredico global de homologacao (Bloco D).
