# Relatório Ciclo 2 · Sprint 10 — Fechamento da frente administrativa (3/3): vínculos tipados Compras ↔ Jurídico + Convênios/Obras (BR-7.8 + BR-10.x)

Data: 2026-10-14 · Trilha: commit `2b9f2e09` (Ciclo 2 S9) + commit desta sprint (migration/manifest/consolidados/C#/suítes/E2E/docs) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

**Etapa 3/3 da frente administrativa** sobre os contratos BR-7.x/S8/S9: os vínculos tipados que
restavam entre Compras e os módulos dependentes. Três frentes num comando só:

- **Jurídico (BR-10.1)** — consulta jurídica tipada (PARECER/IMPUGNAÇÃO/DEFESA/COBRANÇA) vinculada
  ao processo de contratação com prazo de resposta, custo/honorário e chave de idempotência;
  parecer com conclusão FAVORECIMENTO/IMPROCEDÊNCIA/CONDICIONADO e condicionantes; satisfação de
  condição com justificativa; filial por recurso (1 filial/pai, herda o objeto original);
  **gate: homologação do processo bloqueada enquanto houver condição aberta**.
- **Contencioso (BR-10.2)** — abertura ativo/passivo por origem tipada
  (CONTRATO/CONVENIO/TRABALHISTA/TRIBUTARIO/OUTRA) com valor da causa e eventos append-only;
  marcador JURIDICO no contrato **via ledger** (movimento append-only); um contencioso ativo por
  origem; prorrogação de contrato bloqueada com contencioso ativo; desfecho
  (TRANSACAO/JUGADO/DESISTENCIA) com custas e justificativa que encerra e QUITA o marcador.
- **Convênios/Obras (BR-7.8 + BR-10.3/10.4/10.5)** — contrapartida derivada com chave idempotente
  e teto pelo valor de contrapartida do instrumento (regra do contrato, sem parâmetro global);
  vínculo convênio→contrato exigindo contrapartida REALIZADA; prestação de contas PARCIAL/FINAL
  com execução mínima **paramétrica lida do banco**; encerramento de instrumento bloqueado por
  contrato vinculado ATIVO e com saldo de execução exigindo justificativa (ENCERRADO imutável);
  contrato de obra exigindo projeto APROVADO (Lei 14.133, art. 185); homologação de medição
  sequenciada, limitada pela medição física (% e valor) e pelo valor do contrato.
  Auditoria consolidada por GET /vinculos (ledger `compras_vinculo_externo` com quitação
  justificada).

Regras aplicadas: 2–5 (.NET 10/Dapper/PG16 sem EF Core; PK bigint identity; UUID legado intacto),
6–7 (migration idempotente + manifest + consolidados sincronizados), 11/12 (banco como
autoridade — parâmetro, catálogo de permissões e tabelas canônicas dos módulos dependentes; nada
mock/fallback), 13 (**51 falhas nomeadas novas** — 8×404, 18×400, 25×409; nenhuma falha
silenciosa), 14 (extensão das classes existentes; sem nova classe de teste), 20 (RC própria,
fora da RC50.68A) e 21–24 (fixture multi-esfera FEDERAL — regra 22/23; parâmetros por esfera;
nada municipal hardcoded).

## O que foi entregue

1. **Migration idempotente `20261013160000_evo_c2s10_vinculos_juridico_convenios_obra_3_2.sql`**
   aplicada **3×** via psql `ON_ERROR_STOP=1` exit 0 no PG16 (1× antes da primeira rodada + 2× de
   reconfirmação idempotente no fechamento): 3 tabelas novas com PK bigint identity (regra 4 —
   `compras_juridico_consulta_vinculo`, `juridico_parecer_condicao`, `compras_vinculo_externo`);
   7 índices (4 únicos parciais de idempotência/filial); relaxamentos não-destrutivos em 3
   módulos legados (regra 5 — `juridico_consulta.consulta_pai_id`+FK auto+custo_honorario+
   prazo_resposta, `juridico_processo.origem_tipo` com CHECK de domínio recriado idempotentemente
   + `origem_id`, `convenio_contrapartida.chave_idempotencia`); 1 parâmetro do módulo
   `CONVENIOS` (`CONVENIO_PRESTACAO_EXECUCAO_MINIMA_PCT` = 70 — banco como autoridade, regra 11);
   6 chaves novas no catálogo oficial `sigov.permissao` com vínculo SUPERADMIN padrão S7
   (reutilizadas sem seed: `compras.contrapartida.registrar`, `obras.medicao.registrar`,
   `compras.contrato.execucao.fiscalizar`, `compras.auditoria.visualizar`, `homologacao.executar`,
   `contrato.prorrogar`); pós-condição `C2S10-POSTCOND` com falha explícita se faltar qualquer
   artefato (regra 13).
2. **Manifest + consolidados** — entrada `20261013160000` com checksum canônico
   `af23239eb2941ce41279e8026061c64668e828cf4cb38464664d62139fb54566` → manifest com
   **237 entradas**; consolidados regenerados pelo gerador oficial (estado novo declarado:
   "232 incluídas, 4 excluídas") e `-Verify` exit 0 ("Scripts completos estão sincronizados").
3. **C# — extensão apenas das classes existentes** (regra 14):
   - `ComprasIntegradasContracts` — **+51 falhas nomeadas** (8×404, 18×400, 25×409) +
     requests/DTOs S10 (`ConsultaJuridicaDto`, `CondicaoSatisfeitaDto`, `ContenciosoDto`,
     `VinculoExternoDto`, `ConvencioEncerradoDto`, `PrestacaoDto`, `MedicaoHomologadaDto`, ...);
   - `ComprasIntegradasRepository` — **+13 operações Dapper** (12 comandos transacionais, 1 ato =
     1 transação, guard de schema por tabela tocada + 1 leitura só-leitura do GET), lendo as
     **tabelas canônicas dos módulos dependentes como autoridade** (jurídico/convênios/obras) e
     gravando vínculos/movimentos append-only nos ledgers próprios;
   - `ComprasIntegradasController` — **+13 endpoints** na rota existente (12 POST + 1 GET
     `/vinculos`), autorização por chave pontuada do módulo do ato (`juridico.*`/`convenios.*`/
     `obras.*`/`compras.*`) resolvida pelo banco, fail-closed (regra 11).
4. **Suítes** — extensão de teste existente (`SaasComercialTests`): fato de integridade do
   manifest `Be(237)` + trilha comentada; resultado **851/851 verde**.
5. **TIER2 E2E** — bloco S10 em `scripts/c1_tier2_e2e.ps1`: **+17 checks** (base 403 → total
   420 por rodada), fixture fictício `C2S10-%` (entidade **esfera FEDERAL** — regra 22; usuário
   `c2s10.admin` na cadeia do perfil 910401; CVN1 4000 = 3200 repasse + 800 contrapartida c/
   execução 75%; CVN2 1500 = 1000 + 500 c/ execução 40%; obra 10000 com projeto
   EM_ELABORACAO; medições M1–M4; CC `C2S10-%`) + grants dedicados de **17 chaves oficiais** ao
   perfil-sentinel `cf58c2ab-1c20-4a90-88f8-0c2000000010` (cobertura 17/17 assertada ao vivo);
   auto-limpeza cobrindo as 26 tabelas tocadas + contadores finais (`restantes=0`).

## Cenários de prova do roadmap — todos executados ao vivo (PG16)

| # | Cenário | Prova ao vivo |
|---|---|---|
| 1 | Consulta jurídica tipada + idempotência | criar 200 / replay 200 mesmo id / mesma chave corpo diferente → 409 nomeada / filial de pai não resolvido → 409 / filial de pai resolvido → 200 ref `<pai>-FILIAL` |
| 2 | Parecer CONDICIONADO gate da homologação | 2 condições abertas → homologar 409 `PARECER_CONDICAO_ABERTA` / satisfazer c/ justificativa ×2 → homologar 200 |
| 3 | Contrato de obra exige projeto APROVADO (art. 185) | projeto EM_ELABORACAO → 409 `OBRA_CONTRATO_SEM_PROJETO` / APROVADO → 200 valor 1500.00 + vínculo `VC-C2S10-<id>` ATIVO |
| 4 | Contencioso marca JURIDICO no ledger e trava prorrogação | origem JUDICIARIO → 400 nomeada / CONTRATO PASSIVO causa 12000 → 200 + movimento ABERTURA append-only + marcador LEDGER / duplicar origem → 409 / execução reflete `contenciosoAtivo=true` / prorrogar → 409 `PRORROGACAO_BLOQUEADA_CONTENCIOSO_ATIVO` |
| 5 | Desfecho append-only quita o marcador | HOMOLOGADO → 400 nomeada / TRANSACAO custas 2500 → 200 (ENCERRADO + marcador QUITADO + justificativa registrada) / 2º desfecho → 409 `CONTENCIOSO_JA_DESFECADO` / prorrogar liberado → 200 banco `VIGENTE\|2028-11-09\|1` |
| 6 | Contrapartida idempotente c/ teto do instrumento | valor inconsistente → 400 / replay 200 / mesmo idempotency corpo novo → 409 / excede valor do instrumento → 409 / REALIZADA deriva estado |
| 7 | Vínculo convênio exige contrapartida REALIZADA | antes da contrapartida → 409 / depois → 200 / 2 instrumentos no mesmo contrato aceitos / probes de conflito → 409 |
| 8 | Prestação de contas c/ mínimo paramétrico do banco | CVN2 (40% < 70% do parâmetro) → 409 `PRESTACAO_EXECUCAO_MINIMA_NAO_ATINGIDA` / CVN1 (75%) PARCIAL → 200 / CVN1 FINAL c/ saldo 800 → 409 nomeada |
| 9 | Encerramento em cascata + imutabilidade | contrato vinculado ATIVO → 409 `BLOQUEADO_CONTRATO_ATIVO` / quitar vínculo c/ justificativa → encerrar → 400 saldo 600 sem justificativa → 200 com justificativa (ENCERRADO COM SALDO) / re-encerrar → 409 |
| 10 | Medição homologada ≤ física e ≤ valor | M1 4000 → 200 / replay → 409 / M2 fora de sequência → 409 / M3 6000 fecha 100% de 10000 → 200 / M4 excede → 400 |
| 11 | Auditoria consolidada do vínculo | GET `/vinculos` → exatamente 3 linhas (JURIDICO QUITADO, CVN1 ATIVO, CVN2 QUITADO) lidas do banco |

## Defeitos encontrados NA PRÓPRIA prova (sem máscara)

Progressão das rodadas oficiais: **r1 404/16 → r2 408/12 → r3 410/10 → r4 411/9 → r5 419/1 →
r6 420/420** (corrida oficial final, auto-limpeza `restantes=0`). Cada número abaixo é causa raiz
provada por evidência (corpo da resposta, estado do banco e/ou reprodução ao vivo) — não chute:

1. **C#** — check de existência em `compras_processo` usava colunas inexistes
   (`and ativo and not is_deleted` — a tabela não tem) → erro 42703 → 500 em toda criação de
   consulta (rodadas 1–2). Fix: query id+tenant + filtro de status já canônico.
2. **C#** — 3 typos de coluna contra o schema legado: `status_nova`→`status_novo` no movimento
   jurídico do desfecho (42703); leitura de contencioso por `p.data->>'caso'` quando a coluna é
   `dados` (42703); `updated_by=@U` em update de `convenio_instrumento` (coluna inexistente na
   tabela — 42703 no encerramento) (rodadas 1–2).
3. **C#** — homologação de medição comparava percentual acumulado sem tolerância: M3 fechando
   exatamente 100.000% (6000/10000) falhava na desigualdade estrita contra a medição física
   100.000%. Fix: epsilon canônico de 0.005 pct (rodadas 2–3).
4. **Driver (fixture)** — quatro falhas no bloco de setup: `ON CONFLICT (perfil_acesso_id)` único
   quando a unicidade é composta `(perfil_acesso_id, permissao_id)`; typo `juridico_consuta` em
   12 statements (42P01 relation doesn't exist); status `EM_EXECUCAO` fora do domínio de
   `convenio_projeto.status`; e `id` ambíguo na relida de 13 ids de tabela simples com join
   (42702) (rodadas 1–2).
5. **Driver (cleanup)** — ausência do delete pareado de `financeiro_centro_custo` (ficou só o
   contador final) → resíduo > 0 e linha órfã `C2S10-CC1` (rodadas 1–3).
6. **C# — o maior (o "409 fantasma")** — leitura de DTO através da `NpgsqlTransaction` **já
   commitada** em 6 comandos (criar/replay de consulta, emitir parecer, filial, abrir/desfechar
   contencioso): o Npgsql lançava `InvalidOperationException("Transaction is already completed")`
   e o catch-all do controller a mapeava para 409 — disfarçado de conflito de idempotência com a
   linha já commitada no banco e ledger inconsistente (rodada 3). Provado por **reprodução ao
   vivo** (body do 409 trazendo a mensagem verbatim; commit visível no banco; o SQL da leitura
   executado fora da aplicação retorna 1 linha limpa). Fix canônico do próprio arquivo (a
   convenção já documentada nos blocos S8/S9 avisa exatamente dessa armadilha): leitura dentro da
   transação → commit → retorno.
7. **Driver** — captura dos ids de condição com `id` não-qualificado no join
   condição⋈parecer (42702 "column reference id is ambiguous") → `ON_ERROR_STOP` derrubava o
   psql → captura 0/0 → cascata de 8 checks (satisfier 404, homologação final travada em 409,
   `hoid=0` derrubando contrato/contencioso/vínculos/encerramento) (rodada 4).
8. **Driver** — o check de prorrogação esperava status `PRORROGADO`, mas o comportamento canônico
   (S8, provado na base verde) mantém o contrato `VIGENTE` durante a vigência estendida e apenas
   prolonga `data_fim` + grava rastro append-only em `compras_contrato_historico`
   (`status_novo='PRORROGACAO'`). A asserção foi substituída pela prova completa
   `VIGENTE|2028-11-09|1` (status canônico | fim estendido | exatamente 1 rastro) — o C# está
   correto; o driver estava errado (rodada 5).

## Verificação ao vivo (PG16 p1_dev)

- Migration aplicada **3×** (reaplicações idempotentes de verificação no fechamento) via psql
  `ON_ERROR_STOP=1` — todas exit 0; pós-condição `C2S10-POSTCOND` verde em cada aplicação.
- Gerador de consolidados `-Verify` exit 0 ("Scripts completos estão sincronizados").
- Reprodução ao vivo autocontida (`s10_repro.ps1`): fixture → cadeia jurídica → consulta 200 /
  replay 200 mesmo id / conflito 409 nomeado (body completo) → auto-cleanup oficial `restantes=0`.
- Rodadas oficiais TIER2 como na tabela acima — a corrida final (r6) executou a jornada inteira
  em uma única cadeia sobre apps reais, sem atalho de estado.

## Gates

- Build Release `-warnaserror`: **0 avisos / 0 erros** (revalidado a cada fix C#).
- Suítes: **851/851 verdes** (sem classe nova — regra 14).
- TIER2: **420/420 na corrida oficial (r6)** com `restantes=0`.
- Driver PS parseado `SYNTAX_OK` a cada edição; manifest JSON revalidado; BOM UTF-8 do driver
  íntegro.
- RC própria (regra 20): a fatia não toca itens P0/P1 da RC50.68A (avaliador de autorização,
  troca de contexto e dashboard SuperAdmin seguem fora de escopo).

## Notas de ambiente e resíduos declarados (regra 13, não mascarados)

- **Screenshots do browser seguem BLOCKED herdados** (regra 18) — a fatia é API + serviços;
  jornada web da frente administrativa fica para a sprint das telas.
- Memória livre crônica neste host: apps caem espontaneamente; política de dupla sonda + medida
  de RAM imediatamente antes de cada rodada (apps reiniciados a cada corrida oficial).
- Raça na porta 5001 da primeira sonda estável (000 transiente) — monitorada via wrapper dedicado.
- `check-migration-governance.ps1` falha no host (PS 5.1 sem `-AsHashtable`) — pré-existente e
  declarado; porta oficial = gerador `-Verify` (verde).
- `rc50_25` órfã herdada (não-fault); estado "4 excluídas" do gerador esperado; `p1_dev` sem
  ledger de aplicação (psql direto — padrão EVO); commits ASCII via `-F`.
- Família legado intacta: escritas nas tabelas canônicas dos módulos dependentes + ledgers
  próprios; relaxamentos da migration idempotentes e não-destrutivos (nenhuma conversão
  destrutiva de UUID legado — regra 5).

## Jornada da fatia e gates

Jornada única executada ao vivo (sem atalho de estado): demanda→dispensa→adjudicação
(15×100=1500) → **consulta PARECER K-CJ-1 (custo 250.00)** → parecer CONDICIONADO com 2
condições bloqueando a homologação → satisfazer ×2 (justificativas) → homologar 200 → filial
`<pai>-FILIAL` preservando o objeto → projeto APROVADO → **contrato de obra 1500.00 (vínculo
`VC-C2S10-<id>`)** → **contencioso PASSIVO causa 12000** bloqueando prorrogação → desfecho
TRANSACAO (custas 2500) encerra e QUITA o marcador JURIDICO do ledger → prorrogação liberada até
2028-11-09 (rastro 1) → **contrapartidas 800/800 (CVN1) + 500/500 (CVN2)** REALIZADAS → **vínculos
CVN1/CVN2** (exigindo contrapartida) → **prestação de contas** (mínimo 70% lido do banco: CVN2
40% bloqueado; CVN1 PARCIAL 200; CVN1 FINAL c/ saldo 800 bloqueado) → **encerramento CVN2**
(travado por contrato ATIVO → quita vínculo c/ justificativa → 400 saldo 600 sem justificação →
200 com justificação → imutável) → **medições M1 4000 + M3 6000 fecham 100% de 10000** (M2/M4
bloqueados) → **GET /vinculos = 3 linhas exatas** (JURIDICO QUITADO, CVN1 ATIVO, CVN2 QUITADO) →
auto-limpeza `restantes=0`.

**Frente administrativa completa** (BR-7.x + BR-10.x): mapa (S7) → Compras 1/2 (S8) → 2/2 (S9) →
vínculos jurídico/convênios/obras (S10).

Próximo exato: **Ciclo 2 Sprint 11 — Homologação integrada + fechamento da matriz por
níveis** (BR-11.1 a BR-11.5 do roadmap oficial: jornadas completas por perfil em 2 tenants de
esferas diferentes — municipal + estadual — com a regra 21 observável nos dados (limites/
calendário/autoridade diferindo por esfera), cenários transversais obrigatórios em todas as
superfícies novas (concorrência em regra de unicidade, reenvio idempotente, estorno repetido,
cancelamento com efeito, schema ausente, permissão ausente), matriz de evidência fechada
funcionalidade × nível com apontamento de evidência por célula, relatório de fechamento do
incremento + resumo executivo + prompt do próximo incremento e docs operacionais coerentes
entre si — BR-11.5). **GED permanece por último (Sprint 18).**
