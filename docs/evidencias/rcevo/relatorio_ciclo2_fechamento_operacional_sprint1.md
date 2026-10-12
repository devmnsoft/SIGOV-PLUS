# Relatório Ciclo 2 · Sprint 1 — Fechamento Operacional de Educação: integridade transacional da merenda (estoque × movimento × estorno × idempotência)

Data: 2026-10-12 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **vertical de operação de merenda** passa de CRUD genérico para **integridade transacional** (regra 13: falhas nomeadas explícitas; regras 21–24: tudo continua multi-esfera — a regra é por tenant/produto, nada municipal hardcoded). Base legislativa da fatia: LC 11/1996 c/ Lei 11.947/2009 (PNAE) e LC 114/2021 (FUNDEB) — rastreabilidade cardápio → previsão → consumo → perdas exige que cada baixa de estoque seja imutável, conciliável e compensável exatamente uma vez.

Três comandos tipados novos (regras BR-1.x do roadmap `docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md`):

- **Comando tipado `POST /api/educacao/merenda/estoques/lancar`** com
  `{produtoId, operacao: ABERTURA|ENTRADA|AJUSTE, quantidade, chaveIdempotencia?, motivo?}`:
  - `ABERTURA` só cria a linha de saldo quando não existe linha vigente para tenant × produto
    (BR-1.1); segunda abertura → **409 `ESTOQUE_ABERTURA_EXISTENTE`**.
  - `ENTRADA` exige linha aberta (BR-1.1); sem linha → **404 `ESTOQUE_NAO_ABERTO`**;
    quantidade deve ser positiva (400 `ESTOQUE_OPERACAO_INVALIDA`).
  - `AJUSTE` exige justificativa (BR-1.1); sem motivo → **400 `ESTORNO_MOTIVO_OBRIGATORIO`**;
    qtd ≠ 0 e pode ser negativa.
  - **Movimento + saldo na mesma transação** (BR-1.2): falha em qualquer gravação desfaz tudo.
  - Saldo negativo pós-operação → **409 `SALDO_NEGATIVO_REJEITADO`** (BR-1.7).
  - Linha única por tenant × produto (BR-1.3): backstop índice parcial único
    `ux_merenda_estoque_tenant_produto`; criação concorrente usa lock `for update` + retry único
    convertendo colisão `23505` em acréscimo à linha existente — nunca duplica.
  - `chaveIdempotencia` opcional (BR-1.6, mesmo contrato da distribuição): reenvio com mesma
    chave + mesmo conteúdo → retorna o id do movimento original (efeito único); mesma chave com
    conteúdo divergente → **409 `CHAVE_IDEMPOTENCIA_CONFLITO`**. Backstop índice parcial único
    `ux_merenda_movimento_tenant_chave`.
- **Comando tipado `POST /api/educacao/merenda/movimentos/{id}/estornar`** com `{motivo}` (BR-1.7):
  - Cria exatamente 1 movimento `ESTORNO` (qtd = −base) por movimento-base × produto; backstop
    índice parcial único `ux_merenda_movimento_tenant_estorno_ref`
    `(tenant_id, referencia_id, produto_id) where tipo='ESTORNO'`.
  - Estorno repetido do mesmo movimento → retorna o id do estorno existente (compensação única,
    sem dupla compensação em corrida ou clique duplo).
  - Estorno de estorno, estorno de SAÍA já compensada por estorno de distribuição, ou base sem
    justificativa → falhas nomeadas (`ESTORNO_BASE_INVALIDA`, incluindo o código
    `SAIDA_JA_COMPENSADA_POR_ESTORNO_DE_DISTRIBUICAO`; `ESTORNO_MOTIVO_OBRIGATORIO`).
  - Base inexistente/inativa do tenant → **404 `ESTORNO_BASE_INEXISTENTE`**.
- **Comando tipado `POST /api/educacao/merenda/distribuicoes/{id}/estornar`** com `{motivo}` (BR-1.8):
  - Restaura o saldo de cada item item a item (lock `for update` + `n==1` verificado, BR-1.4),
    grava movimento `ESTORNO` por item, marca a distribuição `CANCELADA` com justificativa;
    documento e itens permanecem íntegros (auditoria append-only).
  - Reestorno → efeito único (status terminal já CANCELADA: idempotente, sem reestouro de saldo).
  - Itens cuja baixa já foi compensada individualmente via estorno de movimento são pulados na
    restauração (nenhum saldo é devolvido duas vezes).
  - Distribuição cancelada sem efeitos pendentes → `DISTRIBUICAO_ESTORNO_INDISPONIVEL` (409).
- **Distribuição com revalidação total dentro da transação** (BR-1.5): cardápio `for update`
  (ativo, período, escopo institucional), dia livre (reenvio detectado pela chave persistida em
  `dados->>'chaveIdempotencia'` + comparação de itens antes de tratar dia ocupado como 409),
  itens restritos ao menu, estoque suficiente — baixa com lock ordenado por produto para evitar
  dead-lock; colisão residual `23505` → `DISTRIBUICAO_DIA_EXISTENTE` (409) como no baseline.
  Reenvio com mesma chave + mesmo conteúdo → retorna o id original **sem nova baixa** (BR-1.6).
- **Guarda de cancelamento genérico** (BR-1.9): o endpoint genérico de status
  (`operacoes/educacao_merenda_distribuicao/{id}/status`) passa a rejeitar transição para estado
  terminal de distribuição que possua movimentos de estoque →
  **409 `DISTRIBUICAO_ESTORNO_OBRIGATORIO`**: compensação só pelo comando tipado. Distribuição
  sem efeitos segue cancelável pelo genérico.
- **Endpoint legado `POST /api/educacao/merenda/estoques`** (BR-1.10) mantém a semântica de soma
  exigida pelo baseline E2E, agora transacional com movimento gravado e conexões sempre
  descartadas; 8 registros paralelos convergem em 1 linha com saldo somado.

## Schema (migration publicada e idempotente — regra 6)

`database/postgres/migrations/20261012110000_evo_c2s1_merenda_estoque_idempotencia.sql`
(checksum SHA-256 do conteúdo normalizado: `f57772efa15c370fa693143ef242c1d7ba1df54068b5d2c1060c2668c98b5f8f`):

- Coluna `chave_idempotencia` em `sigov.educacao_merenda_movimento` + índice parcial único
  `ux_merenda_movimento_tenant_chave` (tenant, chave where not null).
- Índice parcial único `ux_merenda_movimento_tenant_estorno_ref` (estorno único por base ×
  produto).
- Deduplicação idempotente das linhas legadas de estoque (mantém `min(id)`, soma saldos) + índice
  parcial único `ux_merenda_estoque_tenant_produto`.
- POSTCOND explícito `C2S1-POSTCOND`: falha nomeada se qualquer um dos 4 artefatos não existir.

Aplicada **duas vezes** em `p1_dev` (idempotência provada); registro no `manifest.json` com
dependency `["20261011190000"]`, `includeInBaseline: true` e `postConditionSql`; consolidados
regenerados via `generate-script-completop.ps1 -IncludeDevelopmentSeed` (regra 7: migration,
manifest, `script_completo.sql`, `script_completo_dev.sql`, `database/script_completo.sql` e
`script_completop.sql` sincronizados — a migration aparece no `script_completop.sql` com o
POSTCOND e o insert de `schema_migrations`).

## Matriz verificável da corrida oficial (bloco E15 no driver Tier-2)

| Check | Cenário | Esperado |
|---|---|---|
| E15.1 | Criar 3 produtos fixture (`c2s1-nolinha`, `c2s1-semestoque`, `c2s1-race`) | 200/200/200 |
| E15.2 | `ABERTURA` 5 | 200 (linha + movimento ENTRADA na mesma transação) |
| E15.3 | Segunda `ABERTURA` | **409 `ESTOQUE_ABERTURA_EXISTENTE`** |
| E15.4 | `ENTRADA` 10 com chave K2 | 200 (movimento persistido com a chave) |
| E15.5 | Reenvio idêntico (mesma chave + conteúdo) | 200 com **o mesmo id** do movimento (efeito único, sem novo lançamento) |
| E15.6 | Mesma chave K2 com quantidade diferente (20) | **409 `CHAVE_IDEMPOTENCIA_CONFLITO`** |
| E15.7 | `AJUSTE` -3 sem motivo | **400 `ESTORNO_MOTIVO_OBRIGATORIO`** |
| E15.8 | `AJUSTE` -3 com motivo | 200 |
| E15.9 | `AJUSTE` -99 (levaria saldo a negativo) | **409 `SALDO_NEGATIVO_REJEITADO`** |
| E15.10 | `ENTRADA` em produto sem ABERTURA | **404 `ESTOQUE_NAO_ABERTO`** |
| E15.11 | 8 `POST /estoques` legados em paralelo no produto race | OK ≥ 1; banco: **1 linha**, saldo = soma dos OK (colisão converteu em acréscimo, nunca linha duplicada) |
| E15.12 | Distribuição A:3 em 2026-11-13 com chave KD1 | 200 (baixa real no estoque) |
| E15.13 | Reenvio idêntico da distribuição | 200 com **o mesmo id**; conta de baixas `SAIDA` = **1** (sem dupla baixa) |
| E15.14 | `estornar` da distribuição | 200; **saldo restaurado ao valor anterior**, status `CANCELADA`, exatamente 1 movimento `ESTORNO`, documento/itens íntegros |
| E15.15 | `estornar` repetido da mesma distribuição | 200 idempotente; estornos continuam = 1; saldo estável (compensação única) |
| E15.16 | Distribuição A:1 em 2026-11-14 (sem chave) | 200 |
| E15.17 | Cancelamento genérico `CANCELADA` na distribuição com efeitos | **409 `DISTRIBUICAO_ESTORNO_OBRIGATORIO`** |
| E15.18 | `estornar` tipado após o bloqueio | 200 |
| E15.19 | `estornar` do movimento de ABERTURA | 200 (movimento ESTORNO criado) |
| E15.20 | `estornar` repetido do mesmo movimento | 200 com **o mesmo id** do estorno (compensação única) |
| E15.21 | `estornar` do próprio estorno | **409 `ESTORNO_BASE_INVALIDA`** |
| E15.22 | `estornar` de movimento inexistente (99999999) | **404 `ESTORNO_BASE_INEXISTENTE`** |
| E15.23 | Conciliação final: `saldo ≡ soma(quantidade dos movimentos)` do produto | igualdade exata no banco (movimento+saldo na mesma transação, estorno único aplicado) |
| CX | Limpeza ampliada (produtos `c2s1-%` + estoques + movimentos + distribuições do bloco) | restantes=0 |

(Blocos E0–E14 das Sprints 1–10 do Ciclo 1 revalidados na mesma rodada: todos os 150 checks do
baseline devem seguir PASS nesta corrida.)

## Corrida oficial — rodada 1: 161 PASS / 12 FAIL (triagem sem máscara)

Primeira execução viva do bloco E15 (`s1_e2e_official.log`): 161 aprovados / 12 falhas.
A triagem classificou os 12 em três classes — nenhuma foi escondida:

**Classe A — defeitos reais de produto, descobertos na prova e corrigidos (esta sprint):**

1. **`'{{}}'::jsonb` em string comum (22P02 em TODO estorno de distribuição):** no
   `EstornarDistribuicaoAsync`, o cast da constante `auditoria='{{}}'::jsonb` foi escrito com
   a dobra de chaves dos strings interpolados (`$"..."`) dentro de uma string comum
   (`"..."`), onde `{{}}` não é escapado. O SQL recebia `'{{}}'::jsonb` — JSON inválido — e o
   cast de constante é avaliado já na parsing do plano: **todo** `POST /distribuicoes/{id}/estornar`
   respondia **500** `Npgsql.PostgresException 22P02: invalid input syntax for type json`.
   Provaram os checks `estorno.distribuicao-restaura-saldo-e-cancela` e
   `estorno.distribuicao.apos-bloqueio-generico`. Reprodutível por instância isolada da API com
   log de console (`correlationId 400a1b05-...`); corrigido para `'{}'::jsonb` e reprovado em
   reprodução limpa (estorno 200 + saldo restaurado + status CANCELADA + estorno idempotente).
2. **`ExecutarCriarAsync` sem mapeamento de `RegistroNaoEncontradoException` /
   `RecursoNaoDisponivelException`:** wrapper dos comandos que devolvem id (incluindo os dois
   estornos) só continha esses catches na variante de status — id inexistente vencia o método com
   409 nomeado mas perdia o 404 nomeado e virava **500** genérico (regra 13 violada na fronteira
   HTTP). Acrescentados os dois catches (404 nomeado), espelhando `ExecutarStatusAsync`.

**Classe B — defeitos do próprio driver (harness), detectados e corrigidos antes da rodada 2:**

3. **BOM removido do driver pelo splice do E15:** o driver original é UTF-8 com BOM; a escrita
   via `WriteAllText(UTF8Encoding($false))` do splice o apagou, e o PowerShell 5.1 passa a ler
   `.ps1` sem BOM como ANSI — todos os literais acentuados do arquivo corromperam na parse.
   Eram exatamente os 4 FAILs do baseline: `transferencia.mesma-turma-rejeitada`
   (`*Transferência rejeitada*`), `correcao.bloqueada-diario-fechado` (`*Correção bloqueada*FECHADO*`),
   `publicar.matricula-alheia-404-nomeada` (`*não encontrad*`),
   `publicar.sem-lancamentos-400-explicita` (`*Sem avaliações ou frequência*`) — status HTTP certo,
   padrão acentuado errado. O conteúdo UTF-8 do arquivo estava intacto desde o splice; a falha
    estava no cabeçalho do arquivo (histórico completo nas rodadas 2 e 3 abaixo).
4. **Interpolação `$x.text` dentro de strings duplas:** no PowerShell, `"produto_id=$pa.text"`
   interpola apenas `$pa` (stringifica o hashtable como `System.Collections.Hashtable`) e trata
   `.text` como literal — gerava SQL inválido nos checks de saldo/race/movimento/conciliação.
   Convertidos para subexpressão `$($x.text)` (mesma forma que os blocos S5–S10 usam).
5. **Propriedade inexistente `.data` no retorno do `Invoke-Api`:** o helper devolve
   `code/body/outFile/err`; `$resp.data` é sempre `$null` e o check de reenvio
   `($e15enr.data -eq $e15en.data)` passava **vazio** (nulo = nulo). Criado helper `Get-E15Data`
   (parse do JSON do corpo) e os checks agora asserção id real ≠ 0 antes de comparar
   (elimina o falso verde).

**Classe C — cascateados (somente A+B):** `registro-legado.race-paralela` (consulta de banco
com SQL inválido do item 4; as 8 escritas paralelas em si tiveram OK=8), `estorno.movimento-*`
(URL montada com id vazio) e `conciliacao.movimento-x-saldo-fechados`.

## Corrida oficial — rodada 2: 169 PASS / 4 FAIL (E15 integralmente verde)

Aplicadas as Classes A e B, a rodada 2 (`s1_e2e_round2_169p_4f.log`) aprovou **todo o bloco E15
(23/23)**: estorno de distribuição 200 com saldo restaurado e status `CANCELADA`, estorno
repetido idempotente, estorno tipado após bloqueio do cancelamento genérico, estorno de movimento
com compensação única (mesmo id), `ESTORNO_BASE_INVALIDA` 409, `ESTORNO_BASE_INEXISTENTE` 404,
reconciliação fechada (`saldo=7.0000 = soma dos movimentos=7.0000`) e cleanup `restantes=0`.
Os 2 defeitos de produto (Classe A) ficaram assim comprovados ao vivo pela primeira vez.

Restaram exatamente os **4 checks do baseline do item 3** (acentuados). Investigação byte a byte:
a primeira "restauração" do BOM alocara um `byte[] (n+3)` e copiou o conteúdo a partir do índice 3
**sem preencher os 3 primeiros bytes** — o arquivo passou a iniciar com `00 00 00` (três NUL) em
vez de `EF BB BF`. O PowerShell 5.1 exibia o sintoma novo (`CommandNotFoundException` em
`c1_tier2_e2e.ps1:1 caractere:1`, token invisível na linha 1) e seguia parseando o arquivo como
ANSI, porque NUL não é BOM. Correção definitiva: sobrescrita dos bytes 0–2 com `EF BB BF`,
verificada por hexdump (`EF BB BF 23 20 3D 3D 3D`, zero bytes nulos no arquivo inteiro) e por
prova de runtime: os quatro literais foram extraídos dos bytes do driver (codepoints corretos
`U+00EA`, `U+00E7`, `U+00E3`, `U+00F5`) e executados num processo filho isolado via `-File`
compondo o mesmo corpo a partir de codepoints — comprimento de runtime 25 e `MATCH=True`.

## Corrida oficial — rodada 3: 173 PASS / 0 FAIL (corrida oficial)

Com o BOM correto no cabeçalho (`EF BB BF`, zero bytes nulos), a corrida oficial
(`s1_e2e_official.log`, ts=2026-10-12T11:47Z, `DRIVER_EXIT=0`) aprovou **todos os 173 checks**:
os 150 do baseline das Sprints 1–10 do Ciclo 1 revalidados integralmente — incluindo os 4
checks acentuados que fecharam as rodadas 1 e 2 — e o bloco E15 23/23: conciliação
movimento×saldo fechada (`saldo=7.0000 = soma dos movimentos`), race de 8 lançamentos paralelos
→ 1 linha com soma exata (sem duplicata), reenvio com efeito único em lançamento e distribuição,
estorno de distribuição restaurando saldo + `CANCELADA` com compensação única, estorno tipado
após bloqueio do cancelamento genérico (`DISTRIBUICAO_ESTORNO_OBRIGATORIO`), estorno de
movimento com compensação única (mesmo id), `ESTORNO_BASE_INVALIDA` (estorno de estorno) e
`ESTORNO_BASE_INEXISTENTE` nomeadas, e cleanup `restantes=0`. Evidências arquivadas:
`artifacts/c1_tier2_e2e/s1_e2e_round1_161p_12f.log`, `s1_e2e_round2_169p_4f.log` e
`s1_e2e_round3_final_173p_0f.log`.



## Defeitos descobertos na prova e corrigidos (rodadas prévias, sem máscara)

1. **Convenção Npgsql de captura de conflito (build):** a primeira versão do repositório capturava
   o conflito pelo tipo `NpgsqlException.PostgresError`, que não existe na versão do Npgsql deste
   repositório. Alinhado à convenção do código-base (`AlmoxarifadoService.cs`):
   `catch (NpgsqlException pg) when (pg.SqlState is "23505")`. Build Release `-warnaserror`
   reexecutado: 0w/0e.
2. **Análise nula (build, `-warnaserror` CS8602):** `r.Motivo?.Trim()` no caminho de estorno —
   corrigido no mesmo ciclo de build.
3. **Bloco E15 escrito contra assinatura errada do driver (harness, detectado antes da corrida
   oficial):** a primeira versão do bloco usava uma forma posicional/hashtable de `Invoke-Api`
   inexistente no driver (a real é `-Bearer/-Method/-Path/-Base/-Body` com JSON string) e
   variáveis inexistentes. Substituído por inteiro via splice por âncoras antes da execução viva;
   sintaxe PowerShell validada por parser após cada edição (regra 19). Sem efeito sobre produto.
4. **Count do manifest no teste comercial (teste existente estendido — regra 14):**
   `SaasComercialTests.cs` asserciona o número de entradas do `manifest.json` (229); acrescida a
   migration desta sprint → atualizado para 230 na mesma classe existente (sem nova classe).

## Resíduos declarados (regra 13)

- Granularidade do saldo de merenda é **uma linha por tenant × produto** (decisão da fatia;
  expansão multi-depôt — galpão/COPEE — fica para evolução futura com migration própria).
- `chaveIdempotencia` da distribuição é persistida dentro do jsonb `dados` (sem coluna dedicada);
  a do movimento tem coluna dedicada + índice — contrato idêntico nos dois comandos.
- Estorno de movimento só é permitido sobre movimento do próprio tenant vigente; `SAIDA` gerada
  por distribuição e já compensada pelo estorno da distribuição não aceita estorno individual
  (falha nomeada `SAIDA_JA_COMPENSADA_POR_ESTORNO_DE_DISTRIBUICAO`) — compensação sempre única.
- O endpoint legado `POST /estoques` segue gravando `saldoInicial` sem `chave_idempotencia`
  (compatibilidade de contrato com o baseline); a proteção contra duplicidade é o índice único
  parcial + convergência em acréscimo (BR-1.10).
- Herdadas das sprints anteriores (não reintroduzidas): `validate-script-completop` falha em seed
  pré-existente `admin@sigov.local` (já em HEAD); generator reporta as 4 migrations com
  `includeInBaseline:false` como excluídas; `rc50_25` órfã permanece intocada (regra 8); RAM local
  cronicamente esgotada neste host — apps mantidos em shells destacados + corrida oficial atrás
  de sonda de readiness (mesma política operacional das Sprints 8–10).

## Gates da sprint (RC própria — regra 20)

- Build: `dotnet build Sigov.sln -c Release -warnaserror` → 0 avisos / 0 erros.
- Suítes (estendidas, sem novas classes — regra 14): Unit / Api / Integration verdes com o novo
  fact estático `MerendaEstoque_Ciclo2Sprint1_LancamentoEstornoIdempotencia_EIntegridadeTransacional`
  (asserts de migration, repository, contratos, controller e manifest); **executado: 847 verdes
  (Unit 521 / Api 158 / Integration 168, 0 ignorados)**.
- Migration aplicada 2× em `p1_dev` (idempotência) + POSTCOND; manifest e os 5 consolidados
  sincronizados (regra 7).
- Execução viva Tier-2: driver `scripts/c1_tier2_e2e.ps1` com o bloco E15 (checks acima) sobre os
  apps reais (Web :5099 / Api :5100), tenant 910001, login `hom.a.admin` + antiforgery + bearer
  `/Auth/ApiToken`; **executado: 173/173 PASS na corrida oficial (rodada 3 — 150 baseline + 23
  E15), limpeza `restantes=0`, `DRIVER_EXIT=0`** (rodadas 1 e 2 triadas acima).
- Docs: `ULTIMA_EXECUCAO.md`, `BACKLOG_EXECUTAVEL.md`, `MATRIZ_FLUXOS_VERTICAIS.md`,
  `STATUS_REAL_MODULOS.md` atualizados com o nível real da jornada merenda:
  **IMPLEMENTADO + VALIDADO POR TESTES + EXECUTADO EM INTEGRAÇÃO + HOMOLOGADO PELA INTERFACE**
  para o escopo da fatia (lancamentos tipados, estornos, idempotência, guarda genérica) — o
  módulo como um todo não é promovido por endpoint isolado.
