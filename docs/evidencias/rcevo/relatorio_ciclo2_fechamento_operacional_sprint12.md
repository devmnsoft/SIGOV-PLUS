# Relatório Ciclo 2 · Sprint 12 — RH e Previdência (servidores públicos) (BR-12.1 a BR-12.8)

Data: 2026-10-14 · Trilha: `4d2055c0` (Ciclo 2 S1–S11) → `56f27b43` (motor previdenciário + estação Web + menu + driver) → commit desta entrega (4 defeitos de produto descobertos na prova ao vivo + refinamentos do driver e self-healing de ambiente) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16) · Apps reais: Web :5099 / Api :5100 (+ :5001 health)

## Escopo desta sprint

A vertical "RH e Previdência" do servidor público brasileiro, do cadastro ao eSocial, com o motor
de cálculo cobrindo **as regras possíveis da legislação aplicável** (solicitação explícita do
dono do produto):

- **BR-12.1** Cadastro: cargo (classe/padrão/nível, modo subsídio vs vencimento+adicionais),
  servidor (matrícula funcional única por tenant), vínculo (1 ativo por servidor, estágio/cargo em
  comissão marcado), remuneração por item com **natureza parametrizada** (catálogo `previdencia_natureza`:
  incide-previdência/incide-IRRF por natureza — banco é a autoridade, regra 11/12).
- **BR-12.2** Folha mensal determinística: INSS/RPPS por faixas parametrizadas por regime e ano
  (LC 51/84; RPPS alíquota única paramétrica), IRRF **tabela progressiva mensal por ano-base +
  acumulação mês a mês**, 1 folha por servidor×competência (backstop único), reprocessamento =
  nova versão que veta a anterior (ambas auditáveis), fechamento idempotente.
- **BR-12.3** Férias: aquisição automática por ciclo aquisitivo de 12 meses → 30 dias
  parametrizáveis; aviso mínimo 30 dias (art. 84 Lei 8.112/90); fracionamento em até 3 períodos
  (parâmetro); gozo parcial com fração mínima (CLT art. 143); abono pecuniário com mínimo
  (CLT art. 145); terço constitucional separado e auditável (art. 7 VII CF).
- **BR-12.4** Afastamentos: saúde/estudo/maternidade (120 dias CF art. 7 XVIII)/paternidade
  (5 dias)/adoção (120 dias) — duração **paramétrica por tipo**, GED obrigatório (controle TCU),
  regime RGPS/RPPS por opção, devolução automática ao fim do prazo e bloqueio de sobreposição.
- **BR-12.5** 13º salário (Lei 4.749/65 c/c Lei 12.735/2012): 1/12 por mês ≥ 15 dias de serviço;
  1ª parcela até dia 30/11 e 2ª até dia 20/12 (**datas paramétricas**); situação anual persistida;
  2ª parcela exige 1ª registrada (`DECIMO_SEM_PARCELA1`).
- **BR-12.6** Teto constitucional (CF art. 37 XV/XVI c/c EC 103/2019) **por esfera de governo**
  (regras 21–24): municipal (0 = livre, vinculado a vencimento do governador — paramétrico),
  estadual e federal (44161.63); bruto acima do teto **bloqueia o fechamento com rollback
  integral** (`SERVIDOR_ACIMA_TETO`).
- **BR-12.7** Contracheque versionado por competência×versão com demonstração item a item
  (natureza/base/alíquota/fórmula — auditável).
- **BR-12.8** eSocial setor público: lotes idempotentes por competência para
  `ADMISAO_S1005`, `TABELAS_S1000_S1010`, `MOVIMENTACAO_S1006` e `REMUNERACAO_S1200`; validação de
  conformidade pré-envio com mensagens listadas e bloqueio quando há erro
  (`BLOQUEADO_VALIDACAO`); reenvio é idempotente (mesmo lote, sem duplicar).

Mais a **melhoria do menu lateral** pedida pelo dono: o `_Sidebar.cshtml` ficou explicativo e
multi-esfera (grupo "Previdência e Folha" com 9 rotas dedicadas; "Pessoas e Ponto (RH)" separando
folha do legado; "Administração" → "Configurações"; "Operação" → "Suporte e Diagnóstico";
"Recursos Humanos (módulo legado)" desambiguado; itens genéricos Convenios/Defesa/Trânsito/
Educação/ProcessosDigitais qualificados pelo domínio).

## Entrega técnica

| Camada | Artefato |
|---|---|
| Migration | `20261014100000_evo_c2s12_previdencia_folha_legislacao.sql` — idempotente; 16 tabelas `previdencia_*` (PK `bigint identity`); catálogo naturezas (9), faixas INSS RGPS 2025/2026 (4) + RPPS (1), IRRF 2025/2026 (5 faixas/ano, coluna `ano_base`), 17 parâmetros `PREVIDENCIA` (com tetos por esfera), 10 permissões `previdencia.*` (+ SUPERADMIN vinculado), pós-condição DO |
| Manifesto | entry **238** `includeInBaseline:true`; consolidados sincronizados pelos 6 arquivos da regra 7 (gerador `-Verify -IncludeDevelopmentSeed` verde) |
| Application | `Sigov.Application/Rh/Previdencia/PrevidenciaContracts.cs` — requests, DTOs, ~40 exceções nomeadas (regra 13), interface 22 métodos |
| Infrastructure | `Sigov.Infrastructure/Previdencia/PrevidenciaFolhaRepository.cs` — motor Dapper (transações, rollback integral, `GuardAsync` de 15 tabelas → 404 `PREVIDENCIA_NAO_DISPONIVEL`), esfera lida de `sigov.entidade` do contexto do usuário (NULL → 400 `ESFERA_NAO_CONFIGURADA`) |
| Api | `PrevidenciaFolhaController` — 22 endpoints sob `/api/previdencia/*`, 10 permissões pontuadas |
| Web | `PrevidenciaController` + 10 views em `Views/Previdencia/` (estação completa, incluindo `_BaseLegislativa.cshtml` com CF/88 arts. 7/XVII-XVIII e 37 XV/XVI, EC 103/19, LC 51/84, Lei 8.112/90, CLT 129–151, Lei 4.749/65 + 12.735/12, Decreto 3.048/99, Portaria MTP 672/2019) |
| Driver | `scripts/c1_tier2_e2e.ps1` — bloco S12 com **49 checks** (fixture idempotente reentrável; 2 tenants de esferas distintas; valores travados; asserts direto no banco; auto-cleanup) |

## Parâmetros canônicos (módulo PREVIDENCIA — autoridade do banco)

`PREVIDENCIA_REGIME=RGPS`; `TETO_REMUNERACAO_MUNICIPAL=0` / `_ESTADUAL=44161.63` / `_FEDERAL=44161.63`;
`FERIAS_DIAS_POR_CICLO=30`; `FERIAS_INTERVALO_MINIMO_DIAS=30`; `FERIAS_FRAC_MINIMO_DIAS=5`;
`FERIAS_ACONTO_MINIMO_DIAS=5`; `DECIMO_1A_PARCELA_MES=11/DIA=30`; `DECIMO_2A_PARCELA_MES=12/DIA=20`;
`AFASTAMENTO_MATERNIDADE_DIAS=120` / `_PATERNIDADE_DIAS=5` / `_ADOCAO_DIAS=120`;
`CONTRACHEQUE_RETENCAO_MESES=24`. Override por tenant demonstrado ao vivo
(`FERIAS_FRAC_MINIMO_DIAS=7` no tenant estadual, origem `OVERRIDE` visível no endpoint).

## Evidência ao vivo — valores travados (PG16 real, HTTP real)

Fixture: tenant A **municipal** (entidade C2S12A-ENT) / tenant B **estadual** (C2S12B-ENT);
servidores fictícios A(10.000), B(3.000, adm 2023-01-10), E(1.000, desligado), D(5.000),
AB(2.000), C(200.000 — caso teto). Data-base sempre dia 20.

| Servidor | Competência | Bruto | INSS | IRRF (acum.) | Líquido | Nota |
|---|---|---|---|---|---|---|
| A (municipal) | 2026-08 | 10000.00 | 1078.14 | 900.07 | 8021.79 | fechamento 3 cálculos (A+B+E); reprocesso de E: v2 1300.00 VIGENTE vetando v1, v3 restaura 1000.00 (versões auditáveis) |
| A | 2026-09 | 10000.00 | 1078.14 | 900.07 | 8021.79 | fecho idempotente (repetição -> 200 `idempotente=true`); contracheque de setembro travado nesta competência |
| A | 2026-11 | 19166.67 | 2361.48 | 3021.27 | 13783.92 | 13º 1ª p. 9166.67 (n=11 meses ≥15d) |
| A | 2026-12 | 10833.33 | 1194.81 | 1061.32 | 8577.20 | 13º 2ª p. 833.33 → situação ENCERRADA nFinal=12 |
| B (adm 2023) | 2026-09 | 3000.00 | 253.41 | 0 | 2746.59 | isento IRRF (abaixo da faixa) |
| B | 2026-11 | 5750.00 | 614.59 | 178.41 | 4957.00 | 13º p1 2750.00 |
| B | 2026-12 | 3250.00 | 283.41 | 0 | 2966.59 | 13º p2 250.00 |
| D (estadual) | 2026-09 | 5000.00 | 509.59 | 109.22 | 4381.19 | teto estadual ativo não bloqueia (dentro do limite) |
| AB (estadual) | 2026-09 | 2000.00 | 157.23 | 0 | 1842.77 | adm 2025-08 (ano completo p/ 13º) |
| AB | 2026-12 | 2166.67 | 172.23 | 0 | 1994.44 | 13º p2 166.67 |
| C (estadual) | 2026-09 | 200000.00 | — | — | — | **409 SERVIDOR_ACIBA_TETO** (bruto > 44161.63) → folha permanece ABERTA (rollback integral); após desligar, fecha OK |

Férias B (3 ciclos aquisitivos × 30 dias; consumo determinístico do ciclo MAIS RECENTE com
saldo>0): G3 20+40 > saldo 30 → `SALDO_FERIAS_INSUFICIENTE`; G3b 20 dias no ciclo mais recente
(30→10, terço 666.67 = 100×20/3); G4 20 dias > saldo 10 → insuficiente; G1 10 dias encerra o
restante do ciclo (10→0, terço 333.33 = 100×10/3); G2 20 dias + abono 10 consome o ciclo
seguinte (30→0, terço 1000.00 = 100×30/3 — abono também gera 1/3, CLT art. 145); F1/F2/F3 de
5 dias no ciclo restante (terço 166.67 cada; saldo residual 15); F4 = 4º período →
`FRACIONAMENTO_MAXIMO_EXCEDIDO`; gozo de F1 → `GOZO`. Estado final conferido no banco:
`saldo 15 | somatório de terços 2500.01 | GOZO 1 | PROGRAMADA 5` (SQL sem fan-out de join —
ver defeito 9). Aviso 5 dias < 30 → `INTERVALO_FERIAS_INSUFICIENTE`; período invertido →
`PERIODO_INVALIDO`.

Afastamentos: maternidade A 120 dias (paramétrico) com GED + regime RPPS; duração 119 dias →
`DURACAO_AFASTAMENTO_INCONSISTENTE`; sem GED → `DOCUMENTO_GED_OBRIGATORIO`; sobreposição →
`AFASTAMENTO_SOBREPOSTO`; devolução antecipada → `DEVOLUCAO_ANTICIPADA`; paternidade D 4 dias ≠ 5 →
inconsistente, 5 dias OK; adoção 120 dias OK; saúde AB OK.

eSocial: tenant A — ADMISAO_S1005 3 eventos VALIDADO (idempotente ao reenviar: mesmo lote),
TABELAS_S1000_S1010 4 eventos (S-1000 + 3 cargos ativos), REMUNERACAO_S1200 2 eventos; 400 tipo
inválido / 404 lote inexistente / 409 folha não fechada. Tenant B — ADMISAO com 1 evento em erro
(nome curto) → envio 409 `BLOQUEADO_VALIDACAO` + lote persistido `BLOQUEADO_VALIDACAO` com mensagem
listada (falha explicita, regra 13); MOVIMENTACAO_S1006 1 evento VALIDADO; REMUNERACAO set OK / out
409.

Dashboard multi-esfera travado: A (contexto municipal) 3 cargos / 2 ativos / 4 competências
fechadas / 9 cálculos vigentes / 1 ciclo c/ saldo / 1 afastamento ativo / 3 lotes validados /
totalLiquidoÚltimaFolha 11543.79; B estadual coerente. Cross-tenant: 404 nomeados; SemFrente:
403 fail-closed ×3 por chave (regra 11); schema probe (rename/restaurar tabela) → 404
`PREVIDENCIA_NAO_DISPONIVEL` (regra 13 — ausência de schema é explícita, nunca lista vazia).

## Defeitos descobertos na prova (e corrigidos nesta sprint)

1. **Escape de here-string no driver**: o fecho do DO block do fixture S12 ficou `$$$;`
   (dólar solto + par escapada) contra o abre `do $$` → `syntax error at or near "$"` no psql e
   cascata nos 34 checks seguintes. Corrigido para o padrão `$$;` já usado nas sprints 9–11;
   varredura confirmou que nenhuma outra linha do bloco carrega `$` não-escapado.
2. **Quirk PowerShell 5.1**: literal decimal com sufixo `m` (`0.005m`) é rejeitado pelo parser
   como operando direito de operador binário (`-lt`) — reproduzido isoladamente
   (`$x = 1 -lt 0.005m` não faz parse). O helper `Get-S12Near` foi reescrito comparando em
   `double` (`-lt 0.005`), sem perda de precisão para tolerância de centavos.
3. **Consolidados `script_completo_dev.sql` obsoletos** (raiz e `database/postgres`): faltava o
   bloco S12 nos dois, embora o `-Verify` simples passasse (ele só checa os completos).
   Regenerados com `-IncludeDevelopmentSeed`; `-Verify -IncludeDevelopmentSeed` agora verde
   (regra 7 satisfeita nos 6 arquivos).
4. **Pressão de RAM ambiente**: uma rodada intermediária abortou em silêncio no início da S11
   (sem registro de erro no log) com ~340 MB livres enquanto 12 nós MSBuild órfãos ocupavam
   ~1,5 GB; kill dirigido + restart limpo dos apps antes da rodada oficial. Declarado, não
   mascarado (regra 13).
5. **`ON CONFLICT` contra índice parcial (PG `42P10`)**: `AdmitirServidorAsync` usava
   `on conflict (vinculo_id,natureza_codigo) do nothing` contra a unique parcial
   `ux_previdencia_remun_natureza ... where not is_deleted` — o PostgreSQL exige espelhar a
   cláusula parcial, senão devolve `42P10` e o admitir caía em 500 genérico. Corrigido com o
   `where not is_deleted` no ON CONFLICT e os 3 inserts (servidor/vínculo/VENCIMENTO) em
   transação única com rollback integral.
6. **`BeginTransaction` sobre conexão fechada**: a `NpgsqlConnectionFactory.CreateConnection()`
   devolve conexão FECHADA (Dapper só abre na execução de comandos — não em `BeginTransaction`)
   → "Connection is not open" em todo método transacional do motor. Helper `BeginTx` com
   `Open()` explícito, padronizado nos 4 blocos de transação (admissão, fechamento, reprocesso,
   eSocial), consistente com o padrão da codebase (Almoxarifado/Compras).
7. **`tx.Commit()` ausente no reprocessamento**: o novo cálculo era inserido dentro da
   transação mas o dispose a rolbava antes de montar a resposta → 409
   "Sequence contains no elements" e banco inalterado (versão nova desaparecia). Commit
   adicionado após `BackfillDecimoAsync`; v2/v3 do reprocesso de E passaram a persistir.
8. **Parâmetros jsonb expostos com aspas**: `ParametrosEfetivosAsync` usava
   `valor_padrao::text`, que para string jsonb devolve `"RGPS"` (com aspas) enquanto o motor
   lê sem aspas — o endpoint divergia do valor efetivo realmente aplicado. Normalização
   `JsonTextoEfetivo` alinha exposição e cálculo (regra 11: banco continua autoridade).
9. **Cenário de férias do driver incompatível com a regra de fracionamento**: a ordem original
   programava F1–F3 antes de G1–G4, fazendo G1 (15d) cair como 4º período do ciclo já ocupado
   (`FRACIONAMENTO_MAXIMO_EXCEDIDO`) — o cenário contradizia o próprio produto. Reordenado para
   G3/G3b/G4 → G1 (10d) → G2 → F1–F4 preservando todas as intents; além disso: `abrir`
   faltante do A-dezembro no driver (a prova mostrou o 404 nomeado), admissão do AB movida para
   2025-08-01 (ano completo exigido pelos valores travados do 13º: p1 1833.33 / p2 166.67) e
   SQL do estado final reescrito sem fan-out de join (o saldo do ciclo estava sendo
   multiplicado pelo nº de programações do próprio ciclo).
10. **Corte externo de processos dotnet na máquina compartilhada**: outra sessão (projeto
    PlantaoPro) matou os hosts SIGOV ~4 min após o início de uma rodada full (corte abrupto,
    sem exceção na log Serilog, RAM liberada) → 288 checks em cascata HTTP 0. Mitigação
    declarada e comprovada: self-heal no driver (`Ensure-SigovUp` + retry único por chamada) —
    restart ALVO dos hosts SIGOV verificado ao vivo em ~12s, transparente à rodada porque todo
    o estado vive em Postgres. A rodada oficial subsequente passou 485/485 sem nem acionar o
    self-heal.

## Gates

- Build Release `-warnaserror` **0w/0e** (solução completa, incl. Razor).
- Suítes existentes **851/851** (Unit 522 / Integration 171 / Api 158, 0 ignorados) — regra 14
  (nenhuma classe nova de teste).
- **Tier-2 E2E HTTP 485/485** (base 436 + 49 S12) sobre apps reais no PG16, auto-cleanup
  restantes=0.
- PS do driver validado pelo parser a cada edição (regra 19); SQL do fixture aplicado via
  `docker cp` + `psql -v ON_ERROR_STOP=1` (exit 0, 2× idempotente).
- Manifest **238 entradas**; fact `Be(238)` verde; generator `-Verify -IncludeDevelopmentSeed`
  verde (regras 6–7).
- Screenshots BLOCKED (regra 18) — declarado; nível "HOMOLOGADO PELA INTERFACE" assenta em E2E
  HTTP real + HTML renderizado das 10 views (prática declarada desde a S6).
- RC própria desta sprint (regra 20) — fora de RC50.68A.

## Notas de ambiente e resíduos declarados

- Tenants **910001/910002** são do driver neste ciclo; fixture S12 self-contained com pre-clean
  reentrável e auto-cleanup terminal (restantes=0 verificado no banco).
- Senha dos usuários fictícios = hash do admin fictício `910301` (já existente no banco dev);
  nada pessoal real (regras 9–10). Grants sentinel `cf58c2ab-…021–024`; override
  `c2s12-frac-override-e2e`.
- Esfera vem **exclusivamente** de `sigov.entidade` do contexto do usuário (regra 21–24);
  entidades sem esfera → 400 `ESFERA_NAO_CONFIGURADA` (comportamento fail-fast nomeado, provado
  pela estação).
- Legado RH (JSONB sob `api/rh/*`) intocado; a frente nova é aditiva e versionada por versão de
  cálculo.

## Resumo executivo

O SIGOV PLUS opera agora a frente completa de **RH e Previdência do servidor público** para as
três esferas: cadastro parametrizado, folha determinística com INSS/RPPS + IRRF acumulado +
13º paramétrico + teto constitucional por esfera com bloqueio de fechamento, férias
legislativas completas (ciclos, terço, fracionamento, abono), afastamentos com GED e
devolução automática, contracheque auditável e eSocial setor público com validação pré-envio
e lotes idempotentes — tudo com **banco como autoridade** (naturezas, faixas, tabelas,
parâmetros e permissões no PG16), ~40 falhas nomeadas e 49 provas HTTP reais com valores
travados conferidos centavo a centavo. O menu lateral ficou explicativo e multi-esfera.
Risco de produção: baixo — front aditiva sobre schema novo, sem alteração de migrations
publicadas; os dois únicos comportamentos novos globais são o menu e a estação.

## Próxima fatia — Sprint 13 (prompt)

> **SPRINT 13 — Orçamento e Responsabilidade Fiscal (LRF).** Legislação: Lei 4.320/64, LC
> 101/2000 (arts. 20, 26, 32, 51, 54), CF art. 212 (MDE 25% por esfera), leis estaduais/orgânicas
> parametrizadas. Entregues (mesmo padrão S12: migration idempotente + consolidados, camadas,
> suítes estendidas sem classe nova, bloco E2E no driver c/ valores travados, docs, commit
> ASCII): BR-13.1 estrutura função→subfunção→programa→ação (projeto/atividade/subprojeto);
> BR-13.2 disponibilidade = dotação inicial + créditos aditivos (sem rollover entre exercícios);
> BR-13.3 empenho (CP) só com disponibilidade suficiente (`EMPENHO_ORCAMENTO_INSUFICIENTE`);
> BR-13.4 liquidação (SL) ≤ empenho disponível (`LIQUIDACAO_SUPER_EMPENHO`); BR-13.5 limites LRF
> monitorados **por esfera** (despesas de pessoal executivo/legislativo/judiciário; restos a pagar;
> 40% em função social; despesas correntes) com alerta e bloqueio paramétricos; BR-13.6 previsão
> bimestral (LC 101 art. 51) gerada automaticamente a partir dos dados reais; BR-13.7 RREO
> (LC 101 art. 54) semi-anual/anual com conjuntos mínimos; BR-13.8 competência fechada (exercício
> encerrado) bloqueia empenho/liquidação nova. Integração prevista: pagamento da folha S12
> consome disponibilidade e alimenta tesouraria (S14). Cenários de prova: empenho que excede
> disponível, liquidação > empenho, MDE abaixo de 25% alertando, exercício encerrado bloqueando,
> RREO gerado com dados reais do driver.
