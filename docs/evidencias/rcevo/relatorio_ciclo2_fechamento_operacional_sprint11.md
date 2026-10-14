# Relatório Ciclo 2 · Sprint 11 — Homologação integrada + fechamento da matriz por níveis (BR-11.1 a BR-11.5)

Data: 2026-10-14 · Trilha: commit `6258cbee` (Ciclo 2 S10) + commit desta sprint (driver/homologação + 2 fixes C# + docs) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14) · Apps reais: Web :5099 / Api :5100 (+ :5001 health)

## Escopo desta sprint

Encerramento do incremento "Fechamento operacional de Educação + frente administrativa" (Ciclos 1+2,
Sprints 1–11): **homologação integrada ao vivo + fechamento da matriz de evidência por níveis**. Não é
uma sprint de código novo — é a sprint que prova, num único pacote executável, que o incremento inteiro
funciona como um sistema:

- **BR-11.1** jornadas completas por perfil em **2 tenants de esferas diferentes** (municipal + estadual)
  com a regra 21 observável nos dados (mesmo ato → derivações diferentes por parametrização do banco);
- **BR-11.2** cenários transversais obrigatórios nas superfícies novas: concorrência em regra de
  unicidade, reenvio idempotente, estorno repetido, cancelamento com efeito, schema ausente, permissão
  ausente;
- **BR-11.3** matriz de evidência fechada por funcionalidade × nível, com apontamento por célula;
- **BR-11.4** este relatório + resumo executivo + prompt do próximo incremento;
- **BR-11.5** `ULTIMA_EXECUCAO`, `BACKLOG_EXECUTAVEL`, `MATRIZ_FLUXOS_VERTICAIS` e
  `STATUS_REAL_MODULOS` atualizados e coerentes entre si.

## O que foi entregue

1. **Fixture S11 idempotente e self-contained** (`scripts/c1_tier2_e2e.ps1`, bloco `Add-Check 'S11'`):
   pre-clean em cascata + inserts + pós-condições explícitas (regras 9–13) sobre os tenants 910001
   (entidade esfera **MUNICIPAL**) e 910002 (entidade esfera **ESTADUAL**): entidades, grupos/perfis
   dedicados por tenant (`C2S11A-*`/`C2S11B-*`, tesouraria/jurídico/administração/SemFrente), 8 usuários
   fictícios (`c2s11a.*`/`c2s11b.*` — regra 10), grants sentinel `cf58c2ab-…011–016` (52 linhas),
   override do parâmetro `RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS` 5→10 no tenant estadual
   (`parametro_modulo_valor` — banco autoridade, regra 11) e auto-cleanup terminal.
2. **16 checks E2E novos** cobrindo BR-11.1/BR-11.2 (nomes exatos na matriz
   `docs/execucao/MATRIZ_EVIDENCIA_FECHAMENTO_OPERACIONAL.md`), executados na mesma rodada sobre a base
   regressiva integral (420 checks dos Sprints anteriores) — **436 checks no total**.
3. **2 correções de produto descobertas NA PRÓPRIA prova** (seção abaixo) — nenhuma máscara: a prova
   achou dois defeitos reais de C# que nenhum teste anterior cobria (lista agregada + status code do
   estorno sem justificativa).
4. **Matriz de evidência** `docs/execucao/MATRIZ_EVIDENCIA_FECHAMENTO_OPERACIONAL.md` (BR-11.3).
5. **Docs operacionais coerentes** (BR-11.5): os 4 arquivos de `docs/execucao/` atualizados nesta
   mesma commit.

## Cenários de prova do roadmap — todos executados ao vivo (PG16)

Corrida oficial **rodada 7 = 436/436 PASS** (ROUND_EXIT=0), apps reais, tenant municipal 910001 +
estadual 910002, auto-limpeza `restantes=0` nos dois cleanups (global + S11):

- **Jornada municipal (910001)**: demanda 20 un × 100.00 = 2000.00 → aprovação (processo único `PI-…`)
  → **cancelamento da demanda aprovada bloqueado com efeito** (409 `DEMANDA_APOS_APROVACAO_IMUTAVEL`)
  → dispensa art. 75 (sem base legal 400 nomeada) → adjudicação → homologação → contrato VIGENTE 2000.00
  → fornecimento 15 un → **recebimento PARCIAL 14+1** (rejeição motivada; prazo 2026-11-19 = 2026-11-12 +
  **5 dias úteis lidos do banco**) → desfecho único SUBSTITUIDO (2º → 409) → fatura NFE conferida →
  **obrigação 1400.00 (só o aprovado)** → liquidação → **destino estoque** (movimento único referenciado
  `FORN-<f>-REC-<r>`, replay idempotente) — jornada **fecha com efeito real no almoxarifado**.
- **Jornada estadual (910002)**: MESMA cadeia ponta a ponta com administração própria do tenant
  (demanda 15 × 80.00 = 1200.00, contrato VIGENTE, fornecimento + reenvio idempotente, recebimento
  **13+2 com prazo 2026-11-26 = override 10 dias úteis do tenant**, obrigação 1040.00 LIQUIDADA).
- **PROVA DA REGRA 21 NOS DADOS**: o mesmo ato (recebimento com rejeição em 2026-11-12) produz prazos
  **diferentes** (2026-11-19 × 2026-11-26) exclusivamente pela parametrização lida do banco — zero
  comportamento municipal/estadual hardcoded (regras 21/24), verificado com sonda fresca no ponto da
  asserção (`municipal|estadual|PADRAO|10`).
- **Perfis modais**: tesoureiro executa fatura→obrigação→liquidação e recebe **403** ao tentar criar/listar
  demanda; jurídico executa consulta PARECER + parecer FAVORECIMENTO e recebe **403** em demanda
  (autorização 100% banco, fail-closed, regra 11).
- **Concorrência real**: 2 clientes simultâneos na mesma chave de unicidade → **exatamente 1
  fornecimento no banco** (respostas 200 idempotente/409 nomeada; r7: 200/200, banco `1|54`).
- **Isolamento cross-tenant**: listagem municipal vê só marcadores `C2S11A-`; leitura cruzada → 404
  `DEMANDA_INEXISTENTE` (escopo antes da busca); contagem cruzada no banco = 0.
- **Schema ausente**: `compras_fornecimento` renomeada → GET+POST respondem **404
  `COMPRAS_INTEGRADAS_NAO_DISPONIVEL` nomeada citando a tabela** (falha explícita, regra 13); restaurada
  → 200 imediato, tabela intacta.
- **Permissão ausente**: usuários SemFrente (login viável, acesso à frente ausente) → **403 em todas
  as tentativas, em ambas as esferas**, antes de qualquer acesso a dado.

## Defeitos encontrados NA PRÓPRIA prova (sem máscara)

As rodadas oficiais foram: **r4 = 430/6** (causas raiz diagnosticadas com reprodução ao vivo) →
**r5 = 422 PASS abortada** no check de concorrência → **r6 = 383/53** (S11 integral verde; 53 = base de
Educação corrompida pelo resíduo do r5) → **r7 = 436/436**. Os defeitos, todos corrigidos e documentados:

1. **Login dos usuários "SemFrente" falhava silenciosamente** — o gate de login do Web
   (`AuthController` L88) exige roles>0 **e** permissões>0 por candidato; usuário sem nenhuma permissão
   nunca autentica. Decisão registrada: corrigir no **fixture** (grupos/perfis/grants `SemFrente` com 1
   chave inofensiva **fora** da frente cada), não no avaliador de autorização — regra 20 exige RC própria
   para tocar o avaliador.
2. **`GET /demandas` → 409 genérico em toda lista** — o binder **por nome** do Dapper não casa com os
   aliases lowercased do PostgreSQL na materialização do record `DemandaIntegradaDto`. Fix C#: projeção
   **posicional explícita** de tupla + `Select(new DemandaIntegradaDto(...))` — mesmo padrão já provado
   do `ToDtoAsync` do módulo (que funciona inclusive com colunas `?column?`). Era o único mapeamento
   direto `QueryAsync<RecordDto>` restante do repositório.
3. **Estorno sem justificativa respondia 409 em vez de 400** — `ObrigacaoEstornoSemJustificativaException`
   sem catch dedicado caía no catch-all da família (→ 409). Fix C#: catch dedicado na seção de
   validações → **400 nomeado** (`ObrigacaoJaLiquidadaException` segue 409, como deve).
4. **Concorrência via `Start-Job` falhava silenciosamente sob pressão de RAM** (r4: respostas vazias
   code=0/0 — 2 shells powershell extras morriam sem erro visível). Primeira reescrita via
   `Task.Run([Func[string]]$scriptblock)` revelou a **causa raiz exata** com repro isolado:
   `PSInvalidOperationException: Não há Runspace disponível para executar scripts neste thread`
   (scriptblock PowerShell não roda em thread de pool sem runspace). Implementação final: **dois
   `Process curl.exe` iniciados em sequência rápida** (paralelos de fato; `ReadToEnd` do stdout aguarda
   cada um; zero processo extra, zero runspace extra), com escaping de argumentos e tag
   `CURL_EXIT_<n>` na evidência quando o curl termina != 0. Validado ao vivo isoladamente (401/401 com
   body correto) antes da corrida.
5. **Rodada abortada derruba o driver em `WaitAll` sobre task faulted** (AggregateException sem
   tratamento no ponto novo) — espera defensivizada (a falha individual agora aparece como texto na
   evidência do check em vez de matar a rodada).
6. **Resíduo de rodada abortada corrompe a próxima** — o cleanup global só roda no FIM do driver; a r5
   abortou no meio da S11 e deixou resíduos da base, e a r6 falhou em 53 checks de Educação com
   `more than one row returned by a subquery used as an expression` / "esperava 0 linhas, havia 7".
   Regra operacional registrada: após driver abortado, sanidade do banco (contagens de sentinela) antes
   do relançamento — aplicado na r7 (sanidade `c2s11_users=0`, `merenda_req=0` antes do lançamento).

## Verificação ao vivo (PG16 p1_dev)

- Fixture S11 aplicada **2× consecutivas** via psql `ON_ERROR_STOP=1` exit 0 (idempotência do pre-clean
  provada; nova aplicação recria com ids novos e pós-condições verdes).
- Probes ao vivo durante o diagnóstico (auto-limpantes, `CLEAN_EXIT=0`): usuário temporário sem grupo →
  login 200×2 com `sessao_usuario` vazia (confirmou o item 1); usuário debug com grant de
  `compras.demanda.visualizar` → 409 com corpo exato do Dapper (item 2) → **após o fix: 200 lista vazia
  `{"success":true,"data":[]}`** e 404 `DEMANDA_INEXISTENTE` no detalhe.
- Banco final após a r7: `restantes=0` nos dois cleanups (global + S11); sanidade posterior
  `c2s11_users=0`, `merenda_req=0`.

## Gates

- Build Release `-warnaserror` **0w/0e** (revalidado após cada fix C#, antes das suítes).
- Suítes existentes **851/851** (Unit 522 / Integration 171 / Api 158, 0 ignorados) sobre o binário
  final — regra 14 (nenhuma classe nova).
- **Tier-2 E2E HTTP 436/436** (rodada 7, oficial) sobre apps reais no PG16 — base 420 + 16 S11.
- PS do driver validado pelo parser a cada edição (regra 19); **sem migration nesta sprint** — manifest
  permanece **237 entradas**, fact 237 verde (regras 6–7 satisfeitas sem alteração).
- Screenshots BLOCKED (regra 18) — declarado, não mascarado; nível "HOMOLOGADO PELA INTERFACE" assenta
  em E2E HTTP real + HTML renderizado (prática declarada desde a S6).
- RC própria desta sprint (regra 20).

## Notas de ambiente e resíduos declarados (regra 13, não mascarados)

- **Não existe usuário cidadão neste banco**: `tipo_usuario` ativo ∈ {OPERADOR, ADMINISTRADOR_GERAL};
  o item "cidadão quando exposto" da BR-11.1 é portanto **N/A** — registrado como não-executável, sem
  simulacro (nenhum catálogo/fallback criado para cobrir o gap — regras 11/12).
- **Screenshots do browser seguem BLOCKED herdados** (regra 18): `browser.screenshot` falha
  consistentemente no harness desde a Sprint 6.
- RAM livre crônica no host (~400–700 MB): apps reiniciados imediatamente antes de cada rodada oficial;
  órfãos de `powershell.exe` de rodadas com timeout limpos por kill dirigido (PID pequeno do shell da
  sessão preservado).
- Container Docker `sigov-api` mantido em Exited(139) manualmente para liberar a porta 5001 do app
  local de homologação (padrão herdado).
- `check-migration-governance.ps1` falha no host (PS 5.1 sem `-AsHashtable`) — pré-existente; porta
  oficial = gerador `-Verify`.
- Logs da API/E2E são UTF-16 neste harness (leitura com `-Encoding Unicode`) — convenção documentada.

## Resumo executivo do incremento (BR-11.4)

**O que funciona (provado ao vivo, PG16, apps reais):** todo o incremento "Fechamento operacional de
Educação + frente administrativa" opera ponta a ponta — merenda (estoque×movimento×estorno), biblioteca
(circulação/inventário/ocorrências/histórico), transporte (rota→viagem→presença→ocorrência→custo),
FUNDEB/custos/rateio + avaliação + Educacenso, telas reais (substituição da fachada estática) e a frente
administrativa completa (demanda→procedimento→licitação ME/EPP→adjudicação→homologação→contrato→
fornecimento→recebimento→fatura→obrigação→liquidação→estorno→destino + vínculos jurídicos/convênios/
obras). Cenários adversos comprovados: efeito único sob concorrência, idempotência de reenvio, estados
terminais irreversíveis, autorização fail-closed, erros nomeados sem 500 genérico, dados separados por
tenant/esfera e a parametrização multi-esfera observável no banco (regra 21). Risco de produção do
escopo homologado: **baixo** — a superfície nova é API-first com invariantes de banco (backstops de
unicidade parciais) e nenhum estado é promovido por endpoint isolado.

**O que está BLOQUEADO e por quê:** (1) **screenshots** do browser — ferramenta do harness
(`browser.screenshot`) falha consistentemente (regra 18); evidência substituta nomeada = HTMLs
renderizados + estados de rede + toasts, desde a Sprint 6. (2) **UI interativa do browser sobre as
superfícies da frente administrativa** — a frente S8–S10 é API-first (comandos tipados) e as telas
dedicadas de operação não são escopo deste incremento; a interface real está provada até a camada de
autenticação web (form+antiforgery+cookie → 302 MinhaCentral → ApiToken) e nas telas de Educação (S6).
(3) **GED** — por construção é o último módulo (Sprint 18), salvo adiamento formal. (4) **usuário
cidadão** — não existe `tipo_usuario` cidadão no banco; item N/A registrado, sem simulacro.

**Risco de produção:** a única mudança de código desta sprint (projeção posicional da lista de demandas
+ status code do estorno) foi descoberta pela própria homologação, corrigida com padrão já provado no
módulo e coberta pela rodada integral 436/436 + suítes 851/851; sem migration, o risco de schema é zero
(manifest 237 invariante). O risco ambiental dominante é o do host de homologação (RAM), não do
produto — e foi controlado operacionalmente (sonda de readiness + relançamento com sanidade).

## Jornada da fatia e próximo incremento

**Próximo exato — Ciclo 2 Sprint 12 — RH e Previdência (servidores públicos)** (BR-12.1 a BR-12.8 do
roadmap `docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md`):

> Prompt: implementar a frente RH/Previdência multi-esfera (município/estado/União — regras 21–24):
> (1) cadastro de cargo (classe/padrão/nível), posição e servidor com **matrícula funcional única por
> tenant** e 1 vínculo ativo (estágio/cargo em comissão marcado), remuneração por item com natureza
> paramétrica; (2) folha mensal determinística — base + incorporações − descontos, INSS/RPPS por faixa/
> alíquota **paramétrica por regime**, IRRF por tabela progressiva **por ano** com acumulação mensal,
> **1 folha por servidor×competência (backstop único)**, reprocessamento = nova versão que veta a
> anterior (ambas auditáveis), fechada não aceita retroativo sem estorno justificado; (3) férias —
> aquisição por ciclo, gozo com antecedência paramétrica (default 30), adicional de 1/3 separado e
> auditável, abono pecuniário, saldo negativo bloqueado, fracionamento paramétrico; (4) afastamentos
> (doença RGPS×RPPS paramétrico, estudo, maternidade 120/paternidade 5, saúde) com documentos GED e
> devolução automática ao fim do prazo; (5) 13º — 1/12 por mês ≥ 15 dias, parcelas em datas
> paramétricas (30/11 e 20/12), acúmulo anual e reflexos calculados; (6) teto constitucional **por
> esfera** (falha nomeada `SERVIDOR_ACIMA_TETO` bloqueando o fechamento) + subsídio vs
> vencimento+adicionais como modo por cargo; (7) contracheque versionado por competência com LGPD e
> demonstração do cálculo; (8) exportação eSocial público em lote versionado e idempotente por
> competência (reprocesso reenvia o mesmo lote sem duplicar) com validação de conformidade pré-envio.
> Gates: migration idempotente + manifest/consolidados sincronizados (regras 6–7), PK bigint identity
> (regra 4), parâmetros no banco (regra 11), falhas nomeadas (regra 13), suítes existentes verdes
> (regra 14), Tier-2 E2E HTTP sobre a base **436** + checks novos da frente, cenário de prova obrigatório:
> folha fechada 2× idempotente; IRRF acumulado confere mês a mês; férias com abono e aviso insuficiente;
> afastamento que estende e devolve; teto bloqueia fechamento; lote eSocial reprocessável. RC própria
> (regra 20). GED continua por último (Sprint 18).

**Encerramento do incremento**: Com Educação (C1 S1–S10 + C2 S1–S6) e a frente administrativa
(C2 S7–S11) homologadas integradas, o incremento "Fechamento operacional" **está fechado** — o ciclo 2
segue para o escopo financeiro/RH/orçamentário (S12 em diante).
