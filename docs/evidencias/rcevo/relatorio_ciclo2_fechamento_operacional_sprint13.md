# Relatório Ciclo 2 · Sprint 13 — Orçamento e Responsabilidade Fiscal (LRF) (BR-13.1 a BR-13.8)

Data: 2026-10-15 · Trilha: `5386fbba` (C2S12 fechada) → commit desta entrega (motor orcamentário + estação Web + driver com valores travados + 5 defeitos de produto descobertos na prova ao vivo + 2 ajustes de driver/ambiente) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16) · Apps reais: Web :5099 / Api :5100 (+ :5001 health)

## Escopo desta sprint

A vertical completa de **Orçamento e Responsabilidade Fiscal** para as três esferas de governo
(Lei 4.320/64 c/c LC 101/2000 e CF/88 arts. 212/214), do catálogo funcional ao RREO:

- **BR-13.1** Estrutura orçamentária: função → subfunção (catálogo nacional **no banco** como
  autoridade — regra 12, sem mock) → programa → ação (projeto/atividade/subprojeto) com poder e
  classificação econômica; competência (exercício) por entidade×ano com encerramento/reabertura
  que bloqueia novos movimentos (`ORCAMENTO_EXERCICIO_ENCERRADO`).
- **BR-13.2** Dotações por linha ação×fonte com **disponibilidade = inicial + créditos
  adicionais − reduções − empenhado** (Lei 4.320/64), uma linha vigente por ação×fonte×exercício
  (`DOTACAO_JA_EXISTE`, backstop único), **sem rollover entre exercícios**; créditos adicionais
  suplementar/especial/extraordinário com ato legal e justificativa mínima parametrizada,
  idempotentes por chave.
- **BR-13.3** Empenho (CP/CP_GLOBAL/SUPLEMENTAR) por itens de dotação: só com disponibilidade
  suficiente (`EMPENHO_ORCAMENTO_INSUFICIENTE` com os valores na mensagem), idempotência por
  chave (1 evento = 1 empenho), anulação total com justificativa parametrizada que devolve à
  dotação (`EMPENHO_JA_ANULADO`/`EMPENHO_COM_LIQUIDACAO`).
- **BR-13.4** Liquidação (SL) contra itens de empenho limitada pelo disponível por item
  (`LIQUIDACAO_SUPER_EMPENHO`), anulação total com justificativa parametrizada.
- **BR-13.5** Tetos LRF **parcialmente por esfera de governo** (regras 21–24): 18 seeds canônicos
  (6 chaves × 3 esferas) + override institucional×tenant no banco; apuração versionada
  append-only (situação DENTRO/ALERTA/BLOQUEIO por percentuais parametrizados), rejeição **somente
  na transição para BLOQUEIO** após o movimento (`LRF_LIMITE_BLOQUEADO` — quem já está bloqueado
  continua pagando), reapuração automática pós-movimento (paramétrica) + apuração explícita.
- **BR-13.6** Previsão bimestral (LC 101 art. 51) gerada **somente dos dados reais**, regeneração
  determinística com exatamente 1 linha vigente por bimestre (`BIMESTRE_INVALIDO` fora de 1..6).
- **BR-13.7** RREO semestral/anual (LC 101 art. 54): lotes vigentes por tipo×período + 9
  conjuntos travados (receita prevista/arrecadada/saldo, despesa autorizada/empenhada/liquidada,
  restos do exercício atual/anteriores, restos anteriores liquidados no exercício) com validações
  nomeadas de tipo/semestre/período.
- **BR-13.8** Exercício encerrado bloqueia qualquer movimento orçamentário novo (409 nomeado);
  reabertura com justificativa ≥ mínimo parametrizado persistindo data e motivo.

Mais a estação Web (11 views sob `Views/Orcamento/` com base legislativa citada na tela: Lei
4.320/64 arts. 32–41, LC 101/2000 arts. 19–21/31/51/54, CF arts. 165–169/212/214) e o grupo
multi-esfera no menu lateral.

## Entrega técnica

| Camada | Artefato |
|---|---|
| Migration | `20261015100000_evo_c2s13_orcamento_responsabilidade_fiscal.sql` — idempotente; 18 tabelas `orcamento_*` + 33 índices (PK `bigint identity`, regra 4); catálogo nacional 19 funções/9 subfunções (função 09 SAÚDE c/ `area_saude_educacao` = base do MDE); 18 seeds LRF canônicos + override institucional×tenant; 5 parâmetros `ORCAMENTO`; 13 permissões `orcamento.*` + vínculo SUPERADMIN dinâmico (13 × N perfis); pós-condição `C2S13-POSTCOND`; checksum `8d82e4e5…` |
| Manifesto | entry **238** (`includeInBaseline:true`); consolidados sincronizados pelos 6 arquivos da regra 7 (gerador `-Verify -IncludeDevelopmentSeed` verde) |
| Application | `Sigov.Application/Orcamento/OrcamentoContracts.cs` — requests, DTOs, falhas nomeadas (regra 13) e interface do motor |
| Infrastructure | `Sigov.Infrastructure/Orcamento/OrcamentoLrfRepository.cs` — motor Dapper determinístico (1 ato = 1 transação, rollback integral, `GuardAsync` sobre as 18 tabelas → 404 `ORCAMENTO_NAO_DISPONIVEL`, checagem LRF pré/pós-movimento, apuração versionada append-only, RREO com conjuntos travados) |
| Api | `OrcamentoController` — **27 endpoints** sob `/api/orcamento/*` com autorização fail-closed por chave pontuada (regra 11); mapping 400/404/409 verificado |
| Web | `OrcamentoController` + 11 views em `Views/Orcamento/` (estação completa: estrutura, dotações, créditos, empenhos, liquidações, receita/arrecadação, limites LRF + apurações, previsão bimestral, RREO, exercícios, parâmetros, dashboard) |
| Driver | `scripts/c1_tier2_e2e.ps1` — bloco S13 com **26 checks** de escopo (isolado `-OnlyS13` = 31 c/ env/auth/CX; fixture idempotente reentrável em 2 tenants de esferas distintas, valores travados centavo a centavo, asserts direto no banco, auto-cleanup cirúrgico restantes=0) |

## Parâmetros canônicos (módulo ORCAMENTO — autoridade do banco)

`ORCAMENTO_APURO_LRF_AUTO=true`; `ORCAMENTO_CREDITO_JUSTIFICATIVA_MIN_CARACTERES=10`;
`ORCAMENTO_ANULACAO_JUSTIFICATIVA_MIN_CARACTERES=5`;
`ORCAMENTO_REABERTURA_JUSTIFICATIVA_MIN_CARACTERES=10`;
`ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES=true` (numerator de RESTOS = atual + anteriores — ver
defeito 5). Override por tenant demonstrado ao vivo no endpoint de parâmetros (origem
`OVERRIDE` visível): mínimo de anulacão 30 no tenant municipal A (padrão institucional 5).

Seeds LRF institucionais (18 linhas = 6 chaves × 3 esferas; alvo/alerta/bloqueio):

| Chave | Tipo | Municipal / Estadual / Federal | Base legal parametrizada |
|---|---|---|---|
| `MDE_MIN` | MIN | 25/24/23 · 25/24/23 · 0 (desativada) | CF art. 212 (piso educacional); federal sem piso único unificado (art. 211) |
| `PESSOAL_EXECUTIVO_MAX` | MAX | 50/45/50 (idem nas 3 esferas) | LC 101 art. 20 II |
| `PESSOAL_LEGISLATIVO_MAX` | MAX | 25/20/25 (idem) | LC 101 arts. 19–20 (poder legislativo) |
| `PESSOAL_JUDICIARIO_MAX` | MAX | 15/12/15 (idem) | tetos de despesas de pessoal da justiça |
| `RESTOS_A_PAGAR_MAX` | MAX | 30/25/30 (idem) | LC 101 art. 31 (restos a pagar) |
| `FUNC_SOCIAL_MIN` | MIN | 0/0/0 (desativada nas 3) | LC 101 art. 21 (40% função social) — parametrizada, alvo 0 |

Regra de situação simétrica: MIN → `pct >= alerta` = DENTRO, `pct <= bloqueio` = BLOQUEIO, senão
ALERTA; MAX → `pct <= alerta` = DENTRO, `pct >= bloqueio` = BLOQUEIO, senão ALERTA (igualdade
pertence a DENTRO nos dois sentidos). Nenhum percentual é hardcoded no motor — tudo vem do banco
(regras 11/12, 21–24).

## Evidência ao vivo — valores travados (PG16 real, HTTP real)

Fixture: tenant A **municipal** (entidade C2S13A-ENT, competências 2025+2026) / tenant B
**estadual** (C2S13B-ENT, 2026); usuários fictícios c2s13a.admin/c2s13a.semperm/c2s13b.admin;
grants sentinel `cf58c2ab-…031/032` (13 chaves `orcamento.*` por esfera) e `…033` (1 chave
inofensiva — fail-closed); overrides LRF: RESTOS A 55/75 e RESTOS B 50/75; override de
parâmetro ANULACAO_MIN=30 no tenant A; override PESSOAL_EXECUTIVO bloqueio 45,5 criado durante
a jornada B. Todas as provas abaixo são HTTP real no PG16 (rodada isolada `-OnlyS13` 31/31;
mesmos checks revalidados na corrida oficial full).

| Etapa | Valor travado | Falha nomeada conferida |
|---|---|---|
| Catálogo nacional | 19 funções / 9 subfunções no banco; funcao 09 SAÚDE c/ área saúde-educação + subfuncao 302 vinculada | — |
| 2025 (A): base | receita TRIBUTARIA 1.000.000 prevista + arrecadação integral (replay idempotente sem somar de novo); dotação 1.000.000 FNT-1 | `ARRECADACAO_INVALIDA` (valor zero) |
| 2025 (A): MDE | empenho 235.000 abre na janela (0 → 23,5% entre bloqueio 23 e alerta 24); liquidação integral zera restos; apuração explícita persiste **MDE_MIN ALERTA 23,5000%** (base 1.000.000, atual 235.000) e RESTOS DENTRO (0%); versões append-only ≥ 2 no banco | — |
| 2025 (A): fechamento | encerrar → ENCERRADO; novo empenho → bloqueado; reabrir com justificativa ≥ mínimo paramétrico → ABERTO c/ data+motivo persistidos | `EXERCICIO_ENCERRADO` (409) |
| 2026 (A): estrutura | programa ok + duplicata; ações MDE (saude) e PESSOAL (grupo PESSOAL_E_ENCARGOS) criadas | `PROGRAMA_JA_EXISTE`; `ACAO_TIPO_INVALIDO`; `ORCAMENTO_FUNCAO_INEXISTENTE` (função 99); `ORCAMENTO_SUBFUNCAO_INEXISTENTE` (999) |
| 2026 (A): dotações | MDE 1.000.000 (disponível = autorizado); duplicata mesma ação×fonte bloqueada (sem rollover); PESSOAL 1.000.000 | `DOTACAO_JA_EXISTE` (409, backstop único) |
| 2026 (A): receita | prevista 1.000.000 (base PREVISTA de todos os tetos); arrecadação integral + replay idempotente | `RECEITA_JA_EXISTE`; `RECEITA_NATUREZA_INVALIDA` |
| 2026 (A): empenho A1 | CP 600.000 aprovado (LRF pós-movimento: MDE 0→60% DENTRO, RESTOS 0→60% ALERTA sob 55/75); replay mesma chave → mesmo id | `EMPENHO_SEM_ITENS`; `EMPENHO_TIPO_INVALIDO` |
| 2026 (A): crédito | SUPLEMENTAR 250.000 c/ ato DL 042/2026 APLICADO (replay idempotente) → linha: autorizado 1.250.000, empenhado 600.000, **disponível 650.000** | `CREDITO_TIPO_INVALIDO`; `JUSTIFICATIVA_INSUFICIENTE` (mínimo 10) |
| 2026 (A): teto | empenho 700.000 contra disponível 650.000 | `EMPENHO_ORCAMENTO_INSUFICIENTE` (409 c/ valores na mensagem) |
| 2026 (A): liquidação | SL 400.000 ATIVA; segunda SL 300.000 sobre o item (restava 200.000) | `LIQUIDACAO_SUPER_EMPENHO`; `LIQUIDACAO_SEM_ITENS` |
| 2026 (A): pessoal | 450.000 ok (0→45% = alerta exato → DENTRO pela regra simétrica); +50.000 levaria a 50% = bloqueio institucional | `LRF_LIMITE_BLOQUEADO` na TRANSIÇÃO para BLOQUEIO (chave PESSOAL_EXECUTIVO_MAX) |
| 2026 (A): limites | GET expõe RESTOS origem OVERRIDE_TENANT (55/75) e MDE INSTITUCIONAL (25/24/23) — nenhum percentual hardcoded | — |
| 2026 (A): previsão bimestral | **1.000.000 / 1.000.000 / 2.250.000 / 1.050.000 / 400.000 / saldo 0** (somente dados reais); regeneração determinística, exatamente 1 vigente | `BIMESTRE_INVALIDO` (bimestre 7) |
| 2026 (A): RREO SEMESTRAL 2 | 9 conjuntos travados: prevista 1.000.000, arrecadada 1.000.000, saldo 0, autorizada 2.250.000, empenhada 1.050.000, liquidada 400.000, **restos atual 650.000** (600k−400k+450k), restos anteriores 0 (2025 integralmente liquidado), liq-restos 0 | `RREO_TIPO_INVALIDO`; `RREO_SEMESTRE_INCONSISTENTE`; `RREO_PERIODO_FIM_INCONSISTENTE` |
| 2026 (A): dashboard | 2 ações / 2 dotações / 2 empenhos ativos / 1 liquidação; 2.250.000 / 1.050.000 / 400.000 / **disponível 1.200.000**; receitas 1.000.000/1.000.000; **LRF: 1 alerta (RESTOS 65% sob 55/75) / 0 bloqueio** na última versão apurada | — |
| 2026 (A): parâmetros | endpoint expõe os 5 parâmetros c/ origem observável (PADRAO vs OVERRIDE do tenant A) | — |
| 2026 (A): anulação | justificativa 5 chars contra override 30 → bloqueada (o override muda o comportamento — regra 24); anulação devolve 10.000 à dotação | `JUSTIFICATIVA_INSUFICIENTE` ('minimo de 30'); `EMPENHO_JA_ANULADO`; `EMPENHO_COM_LIQUIDACAO` |
| Transversal A | usuário sem nenhuma chave `orcamento.*` → 403 fail-closed em GET e POST; admin B não vê empenho do tenant A; ano 2024 sem competência → 404; rename temporário de tabela → 404 nomeado e restaura → 200 | `ORCAMENTO_EMPENHO_INEXISTENTE`; `ORCAMENTO_EXERCICIO_INEXISTENTE`; `ORCAMENTO_NAO_DISPONIVEL` |
| B estadual: base | estrutura + dotação 1.000.000 + receita 1.000.000; empenho pessoal EXECUTIVO 480.000 (0→48% entre alerta 45 e bloqueio 50 institucionais = ALERTA, não bloqueia) | — |
| B estadual: override | PESSOAL_EXECUTIVO bloqueio 45,5 (do tenant) muda a apuração: **BLOQUEIO persistido a 48,0000%** (base 1.000.000, atual 480.000); MDE do estado = **BLOQUEIO a 0%** (piso sem empenhos em saúde/educação); limites mostram origem OVERRIDE_TENANT (45,5) e RESTOS orgânico estadual (50/75); empenho 5.000 quando JÁ bloqueado → permitido (negação só na transição) | — |
| B estadual: dashboard | esfera herdada do contexto (nada municipal hardcoded); 1 ação / 1 dotação / 2 empenhos ativos / 0 liquidações; 1.000.000 / 485.000 / **disponível 515.000**; **LRF: 0 alertas / 2 bloqueios** na última versão | — |
| Cleanup | limpeza cirúrgica S13 nos DOIS tenants (18 tabelas por tenant, overrides LRF A/B, override de bloqueio B, override de parâmetro, sessões/auditorias, grants sentinel, usuários, grupos/perfis, competências, entidades) | restantes=0 no banco |

## Defeitos descobertos na prova (e corrigidos nesta sprint)

1. **Materialização Dapper de records posicionais com colunas snake_case multi-palavra**: o binder
   por nome do Dapper casa coluna↔parâmetro do ctor case-insensitive **sem normalizar
   underscores** — `tipo_comparacao` não casa com `TipoComparacao`. Sintoma duplo: com lista
   vazia o defeito fica mascarado (nada para mapear); com linhas existentes, 409 genérico
   ("parameterless default constructor"). Corrigido em **2 lotes** (cada um verificado por
   probe HTTP real antes de prosseguir): lote 1 = aliases camelCase aspas-duplas nos selects
   diretos `SqlDotacaoSelect`, catálogo função/subfunção, listagem de ações, apuração LRF,
   previsão bimestral ×2, RREO lote ×3 e exercícios; lote 2 = `LimiteRow` ×2, onde o alias
   precisa ser o **nome real do parâmetro do record** (`tipo/alvo/alerta/bloq`, não o camelCase
   da coluna) — detectado porque o primeiro alias continuava falhando com a nova assinatura no
   erro.
2. **`CriarDotacaoAsync` — 25P02 dentro do `catch (23505)`**: após a violação de unique a
   transação fica abortada e a query de readback (buscar o código da ação para a mensagem)
   executava dentro dela → `current transaction is aborted` derrubava o catch e qualquer
   **duplicata de dotação respondia 500 genérico** em vez de 409 `DOTACAO_JA_EXISTE`.
   `tx.Rollback()` antes do readback.
3. **`RegistrarCreditoAsync` — `count(1) ... FOR UPDATE`**: PostgreSQL rejeita FOR UPDATE sobre
   agregado (`0A000`) → **todo crédito adicional respondia 500 genérico**. Substituído por
   `select id ... for update` com `QueryFirstOrDefaultAsync<long>` (lock preservado).
4. **`AnularLiquidacaoAsync` — 0A000 latente**: o mesmo padrão `count(1) FOR UPDATE` existia na
   anulação de liquidação (fluxo ainda não coberto por E2E) — corrigido no mesmo passe, sem
   esperar a prova.
5. **`SqlRaAntevigentes` ignorava o exercício atual**: com
   `ORCAMENTO_RESTOS_INCLUI_ANTEVIGENTES=true` (default canônico), a fórmula somava apenas
   empenhos de **exercícios anteriores** (`ano < @A`) — os restos do próprio ano caíam do
   numerador e `RESTOS_A_PAGAR_MAX` ficava travado em 0% (nenhum alerta jamais dispararia,
   enquanto o RREO — SQL próprio — já mostrava 650.000 corretos, provando a divergência).
   Corrigido para atual + anteriores (`ano <= @A`); prova: RESTOS 65% ALERTA no dashboard e no
   RREO coerentes na mesma rodada.
6. **Fixture do driver contra o schema real**: insert em `sigov.exercicio` sem `tenant_id`
   (NOT NULL, sem default) derrubou a rodada 1 em cascata (6P/25F). Corrigido incluindo a
   coluna + `on conflict (entidade_id, ano) do nothing` (unique pré-existente) e o assert DO
   contando exatamente as **13 chaves pontuadas** `orcamento.%` (o catálogo também carrega 8
   permissões legacy `ORCAMENTO_*_MANAGE/VIEW` — a autoridade do motor são as pontuadas).
7. **Ordem de FKs no cleanup do driver**: faltavam os deletes de sessões/auditoria antes de
   `sigov.usuario` (`auditoria_evento_usuario_id_fkey` tornava o delete impossível) — espelhado
   o cleanup S12 (sessões, contextos, auditoria, logins, MFA, tokens, filas, preferências e
   filtros salvos) com verificação restantes=0 no terminal.

Ambiente (declarado, não mascarado — regra 13/18): a técnica de probe via `curl.exe` com corpo
em arquivo falhava de forma inconsistente sob PowerShell 5.1 (quoting/decoração de JSON); a
sonda definitiva usa `System.Net.Http.HttpClient` (scripts locais `s111_probe`/`s113_diag`),
que tornou o diagnóstico determinístico (dumps completos de cada resposta + log Serilog da API
para as stacks 25P02/0A000).

## Gates

- Build Release `-warnaserror` **0w/0e** (solução completa, incl. Razor) — revalidado após cada
  correção do motor.
- Suítes existentes **851/851** (Unit 522 / Integration 171 / Api 158, 0 ignorados), incluindo o
  fact de manifest `Be(239)` — regra 14 (nenhuma classe nova de teste).
- **Tier-2 E2E HTTP 511/511 na corrida oficial full** (base 436 + 49 S12 + 26 S13) sobre apps
  reais no PG16, auto-cleanup restantes=0; bloco isolado `-OnlyS13` **31/31** (rodadas sem
  máscara: r1 6P/25F fixture, r2 10P/21F materialização Dapper lote 1, r3 26P/5F defeitos 2–5,
  r4 isolado 31/31, r5 full 491P/20F — os 20 F todos **HTTP 0** numa janela contínua de telas
  reais E4/E6 [host Web indisponível no ambiente instável, recuperado pelo self-healing do
  driver *dentro da mesma corrida*, com todos os blocos seguintes, inclusive os 26 S13,
  passando], **r6 full oficial = 511/511**).
- PS do driver validado pelo parser a cada edição (regra 19); SQL do fixture aplicado via
  `docker cp` + `psql -v ON_ERROR_STOP=1` (exit 0, 3× idempotente incl. pré-validação
  standalone).
- Migration `20261015100000` aplicada 2× exit 0 no PG16 + `C2S13-POSTCOND` verde; manifest
  **238 entradas** + generator `-Verify -IncludeDevelopmentSeed` verde (regras 6–7).
- Views da estação: 11 rotas Web → 200 (HTML renderizado). Screenshots seguem BLOCKED herdado
  (regra 18) — declarado.
- RC própria desta sprint (regra 20) — fora de RC50.68A.

## Notas de ambiente e resíduos declarados

- Tenants **910001/910002** são do driver neste ciclo; fixture S13 self-contained com pre-clean
  reentrável e auto-cleanup terminal (restantes=0 verificado no banco, nos dois tenants).
- Senha dos usuários fictícios = hash do admin fictício `910301` (já existente no banco dev);
  nada pessoal real (regras 9–10). Grants sentinel `cf58c2ab-…031–033`; overrides
  `c2s13-*` (RESTOS A 55/75, RESTOS B 50/75, ANULACAO_MIN A=30, PESSOAL bloqueio B=45,5).
- Esfera vem **exclusivamente** da entidade do contexto do usuário (regras 21–24); a jornada
  municipal A × estadual B provou que diferença alguma de comportamento vem de código — tudo
  dos parâmetros/seeds lidos do banco (incl. MDE federal desativado via alvo 0, não via ramo).
- RAM flutuante na máquina compartilhada (~2,6 GB); o self-healing do driver (herdado da S12)
  provou-se de novo nesta sprint: na corrida oficial r5 o host Web ficou indisponível por uma
  janela (20 checks consecutivos HTTP 0 em telas reais E4/E6), foi recuperado pelo self-healing
  *dentro da mesma corrida* e a corrida oficial final r6 saiu limpa 511/511 — nenhum produto da
  fatia esteve envolvido (ausência de resposta do host, não 4xx/5xx do app).
- Sem alteração de migrations publicadas (regra 8): todo o schema S13 nasceu nesta RC;
  `script_completop.sql` e os demais consolidados da regra 7 sincronizados pelo gerador.

## Resumo executivo

O SIGOV PLUS opera agora a frente completa de **Orçamento e Responsabilidade Fiscal** para as
três esferas: estrutura função→subfunção→programa→ação com catálogo nacional no banco, linhas de
dotação por ação×fonte com disponibilidade auditável centavo a centavo (sem rollover), créditos
adicionais com ato legal, empenho/liquidação por itens com tetos nomeados, **tetos LRF
parametrizados por esfera** (18 seeds institucionais + override do tenant), apuração
versionada append-only que **bloqueia somente na transição para BLOQUEIO** (quem está bloqueado
continua pagando), previsão bimestral e RREO semestral/anual gerados exclusivamente dos dados
reais com conjuntos travados, e encerramento/reabertura de competência bloqueando movimentos —
tudo com **banco como autoridade** (~30 falhas nomeadas novas, regras 11–13) e 26 provas HTTP
reais com valores travados em duas esferas diferentes. Risco de produção: baixo — front aditiva
sobre schema novo desta própria RC, sem tocar migrations publicadas; os 5 defeitos de produto
(2 de transação abortada, 2 de `FOR UPDATE` sobre agregado, 1 de fórmula LRF) foram encontrados
e corrigidos na prova viva, cada um com reprodução e stack reais, sem máscara.

## Próxima fatia — Sprint 14 (prompt)

> **SPRINT 14 — Tesouraria e Contabilidade (NBC TSP)** (BR-14.1 a BR-14.8 do roadmap oficial).
> Legislação: NBC TSP (equivalência IFRS para o setor público), Lei 4.320 (contas), LC 101
> (divulgação), normas RGF do Ministério da Fazenda, ICMS/ISS para receitas próprias, LGPD.
> Entregues (mesmo padrão S12/S13: migration idempotente + consolidados, camadas, suítes
> estendidas sem classe nova, bloco E2E no driver c/ valores travados, docs, commit ASCII):
> BR-14.1 plano de contas em árvore com natureza contábil/orçamentária (contas de lançamento
> vs de síntese) e mapa contábil×orçamentário configurável; BR-14.2 lançamento contábil com
> **débitos = créditos sempre** (saldo 0, sem linha zerada, contas válidas e abertas) gerado
> por origem com referência e **reprocessamento que não duplica** (backstop único por
> referência); BR-14.3 caixa/bancos por conta×**fonte de recurso** (fontes vinculadas — FUNDEB
> — com restrição de uso conferida e falha nomeada; saldo derivado de lançamentos — ajuste só
> por lançamento de encerramento justificado); BR-14.4 recebimento por título (imposto/taxa/
> convênio/FUNDEB) com parcelas, pagamento parcial, baixa idempotente por comprovante, régua
> de cobrança paramétrica e inscrição em dívida ativa (comando — S19); BR-14.5 conciliação
> bancária assistida (OFX/CSV/CNAB) com **pré-validação e preview obrigatório** antes de
> aplicar, extrato versionado e marcações idempotentes (reprocessar arquivo não duplica);
> BR-14.6 fila de pagamento das obrigações liquidadas (**folha S12 consome a disponibilidade do
> empenho `ORCAMENTO_DESPESA_FOLHA_ID` e alimenta a tesouraria** — integração com S9/S13),
> saída gravada por fonte conferida (fonte errada = bloqueio pré-pagamento), estorno
> idempotente; BR-14.7 fechamento de competência bloqueando lançamentos novos (estornos abertos
> com justificativa) + balancetes versionados (patrimônio/resultado/caixa por fonte) + RGF
> mensal por esfera com origem verificável; BR-14.8 provisões por lançamento
> (provisiona → realiza c/ estorno da provisão + gasto real + diferença registrada). Cenários
> de prova: lançamento desbalanceado bloqueado; reprocessamento de origem → 1 conjunto;
> conciliação reimportada → sem duplicata; fonte FUNDEB em gasto vedado → nomeada;
> fechamento bloqueia lançamento e estorno funciona; pagamento da folha consumindo
> disponibilidade do empenho ORCAMENTO. GED por último (Sprint 18).
