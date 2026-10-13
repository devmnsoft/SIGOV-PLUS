# Relatório Ciclo 2 · Sprint 9 — Compras integradas (2/2): fornecimento → recebimento → fatura → destinação → obrigação financeira

Data: 2026-10-13 · Trilha: commit `fdf1e017` (Ciclo 2 S8) + commit desta sprint (migration/manifest/consolidados/C#/suítes/E2E/docs) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

**Etapa 2/2 da frente administrativa sobre os contratos BR-7.x da S7**: fornecimento contra
item de contrato (BR-7.5a) → recebimento por fornecimento com rejeição/substituição e espelho
legado (BR-7.5b) → fatura/NF-e vinculada com conferência (BR-7.5c) → destinação estoque/
patrimônio via comandos tipados (BR-7.6) → obrigação financeira única e idempotente com
liquidação/estorno (BR-7.7). Regras aplicadas: 2–5 (Dapper/PG16 sem EF Core; PK bigint identity;
UUID legado intacto), 6–7 (migration idempotente + manifest + consolidados sincronizados),
11/12 (banco como autoridade — parâmetros e catálogo lidos do banco, nada mock), 13 (falhas
nomeadas explícitas — 23 nomeadas novas nesta frente, nenhuma falha silenciosa), 14 (estender
suítes existentes — sem nova classe), 19 (sintaxe dos artefatos alterados), 20 (RC própria) e
21–24 (multi-esfera: fixture desta corrida em entidade de esfera **federal** — alternando a
estadual da S8; unidade gestora/executora resolvidas da entidade do banco).

**Sem parâmetros/permissões novas**: a reutilização do catálogo C2S7 é explícita e verificada
ao vivo — chaves canônicas da frente (`compras.fornecimento.criar`,
`compras.fornecimento.visualizar`, `compras.recebimento.aprovar`/`rejeitar`,
`compras.fatura.vincular`, `compras.obrigacao.liquidar`, …) + reuso documentado das chaves do
módulo de destino (`almoxarifado.recebimento.confirmar` para ESTOQUE,
`patrimonio.bem.criar` para PATRIMONIO). Prazo de substituição de rejeição = parâmetro
parametrizado no banco (5 dias úteis do catálogo C2S7) — nada hardcoded (regra 24).

**Fora do escopo (etapa 3/3, Sprint 10):** contrapartidas convênio/obra (BR-7.8) e demais
dependências da frente (jurídico). GED por último.

## O que foi entregue

**1. Migration idempotente** — `database/postgres/migrations/20261013150000_evo_c2s9_compras_integradas_2_2.sql`
(aplicada **3×** via psql `ON_ERROR_STOP=1`, exit 0 — idempotência provada):
- 5 tabelas novas com PK `bigint generated ... as identity` (regra 4): `compras_fornecimento`
  (chave_idempotencia única por tenant), `compras_fornecimento_recebimento` (prazo de
  substituição + desfecho único por rejeição + vínculo `legacy_recebimento_id`),
  `compras_fatura_integrada` (NF-e/NFS-e com natureza/cnpj/conferência),
  `compras_destinacao_integrada` (referência única por tenant — idempotência do movimento
  referenciado) e `compras_obrigacao` (referencia_idempotencia, valor_liquidado, estados
  REGISTRADA/LIQUIDADA/ESTORNADA);
- índices de unicidade/filtro por tenant para cada comando;
- **relaxamentos não-destrutivos do legado** (regra 5 — nada destrutivo):
  `compras_recebimento.processo_id`/`processo_item_id` viram nullable (o espelho S9 não nasce
  de processo), nova coluna `compras_recebimento.fornecimento_id` e `ck_compras_receb_status`
  alargado para aceitar `APROVADO|PARCIAL|REJEITADO` ao lado do domínio legado
  (`PENDENTE|INTEGRADO|CANCELADO`) — mesmo padrão de relaxamento dual-geração da S8 seção 1c;
- pós-condição explícita `C2S9-POSTCOND` (regra 13): falha nomeada se faltar qualquer tabela,
  índice, coluna ou estado relaxado.

**2. Manifest + consolidados** — entry `20261013150000` (checksum SHA-256
`e3b4252c19513b7477381bd2a066e611268bf7731571f9f8722f971fc94a7d49`) → manifest **235 entradas**;
consolidados (`script_completo.sql`, `script_completo_dev.sql`, `database/script_completo.sql`,
`script_completop.sql`) regenerados pelo gerador oficial e **verificados**
(`scripts/generate-script-completop.ps1 -IncludeDevelopmentSeed` + `-Verify` — exit 0; "excluídas
4" herdado, não-fault).

**3. C# (estendidos das classes existentes da S8 — regra 14 na família de testes; nenhuma classe
nova de serviço/controlador)**:
- `src/Sigov.Application/Compras/Integradas/ComprasIntegradasContracts.cs` — +requests/DTOs/
  exceções nomeadas desta frente: 404 (`FornecimentoInexistente`, `ContratoItemInexistente`,
  `RecebimentoIntegradoInexistente`, `ObrigacaoInexistente`, `DestinoLocalInexistente`,
  `DestinoMaterialInexistente`); 400 (`RECEBIMENTO_REJEICAO_SEM_MOTIVO`,
  `RECEBIMENTO_DESFECO_INVALIDO`, `FATURA_NATUREZA_INVALIDA`, `FATURA_FORNECEDOR_DISCREPANTE`,
  `FATURA_VALOR_DIVERGENTE_DO_PEDIDO`, `DESTINO_TIPO_INVALIDO`); 409 (`FONTECIMENTO_EXCEDE_ITEM_CONTRATO`,
  `FORNECIMENTO_CHAVE_IDEMPOTENCIA_CONFLITO`, `RECEBIMENTO_ACIMA_FORNECIDO`,
  `REJEICAO_DESFECO_JA_REGISTRADO`, `FATURA_JA_VINCULADA_CONFLITO`, `DESTINO_EXCEDE_QTD_APROVADA`,
  `DESTINO_REFERENCIA_CONFLITO`, `DESTINO_LOCAL_INATIVO`, `OBRIGACAO_FORNECIMENTO_INCOMPLETO`,
  `OBRIGACAO_DUPLICADA_REFERENCIA`, `OBRIGACAO_LIQUIDACAO_SEM_FATURA`, `OBRIGACAO_JA_LIQUIDADA`
  + estorno sem justificativa) — todas herdando a base da frente com catch-all 409;
- `src/Sigov.Infrastructure/Compras/ComprasIntegradasRepository.cs` — +9 comandos Dapper/PG16,
  transação por ato, guards de schema: **fornecimento** (valor DERIVADO = quantidade × unitário
  do item do contrato e fornecedor DERIVADO do contrato — nunca editáveis livres; disponibilidade
  do item conferida na criação; idempotência por chave com replay de efeito único e conflito
  nomeado), **recebimento** (aprovado+rejeitado ≤ fornecido restante; rejeição exige motivo;
  prazo de substituição = data_recebimento + parâmetro de dias úteis do banco (fins de semana
  pulados); espelho na linha legado `compras_recebimento(_item)` com documento
  `REC-FOR-{recId}` mantendo a execução contratual S8 consistente; máquina do fornecimento:
  `PARCIALMENTE_RECEBIDO`/`CONCLUIDO` — só fecha quando tudo recebido E todas as rejeições com
  desfecho; desfecho `SUBSTITUIDO|ACATADO` único por rejeição), **fatura** (natureza
  NFE/NFS-e; CNPJ do emitente = CNPJ do fornecedor derivado; valor = valor_total do
  fornecimento com divergência nomeada; idempotência por fornecimento), **destinação**
  (idempotência por referência com conflito de conteúdo nomeado; destino total ≤ quantidade
  aprovada do recebimento; ESTOQUE = 1 `almoxarifado_movimentacao` ENTRADA com motivo
  `FORNECIMENTO` e `documento_origem='FORN-{f}-REC-{r}'`; PATRIMONIO = 1 `patrimonio_bem` no ato
  com `codigo_tombo='PAT-FOR-{destId}'`, contrato/NF/data do recebimento; local inexistente/inativo
  bloqueia antes de qualquer escrita), **obrigação financeira** (nasce só de fornecimento
  CONCLUIDO; valor = SOMENTE o aprovado × unitário — rejeições fora; 1 fornecimento aprovado =
  1 obrigação, duplicidade por referência diferente nomeada; liquidação exige fatura conferida e
  é terminal; estorno por compensação com justificativa — estado terminal, sem delete);
- `src/Sigov.Api/Controllers/ComprasIntegradasController.cs` — +9 endpoints sob o mesmo route
  `[Route("api/compras/integradas")]`, autorização fail-closed por chave pontilhada da seed S7
  no banco (regra 11) — incluindo `RunMulti` (recebimento misto consome aprovar+rejeitar) e
  `AllowedMod` (destinação consome as chaves do módulo próprio, `almoxarifado`/`patrimonio`),
  sem alterar os mapeamentos 404/400/409 já auditados da S8 (P0 preservado).

**4. Suítes estendidas (regra 14)** — `SaasComercialTests` (classe existente): asserção do
manifest 235→236 segmentos + comentário de trilha da entrada C2S9. Resultado: **851/851 verdes**
(Unit 522 / Integration 171 / Api 158, 0 ignorados).

**5. TIER2 E2E HTTP real (driver `scripts/c1_tier2_e2e.ps1`)** — bloco **S9 com 21 checks**
(fixture fictício `C2S9-%` no tenant 910001 com auto-limpeza em cascata incluída nos
`restantes` globais): jornada completa por HTTP real Web :5099 → ApiToken → bearer Api :5100 →
Dapper → PG16, com **usuário dedicado `c2s9.admin`** vinculado a **entidade fixture de esfera
federal** (regras 21–24) e concessão das chaves canônicas da frente ao perfil (mesma
mecânica sentinel `cf58c2ab` da S8 — autorização 100% resolvida pelo banco). Fluxo numérico:
item do contrato **18 un × 100,00 = 1.800,00**; fornecimentos f1=12 / f2=4 / f3=2 com
disponibilidade conferida passo a passo (fA=7 e fB=3 e fC=1 → 409 nomeados); f1 com
recebimento parcial (aprov 6 / rej 2 c/ motivo, prazo 2026-11-19 = 5 dias úteis de 2026-11-12),
desfecho SUBSTITUIDO e fechamento; fatura NF-C2S9-001 conferida; obrigação OB-C2S9-F1 =
**1.000,00** (somente o aprovado — rejeição excluída) liquidada e OB-C2S9-F2 = 400,00 estornada;
destinações r1→estoque (movimento único referenciado, replay idempotente) e r1→patrimônio
(ficha no ato) com bloqueios nomeados; execução contratual final coerente (totalFornecido
1.600,00 = 88,89%, bloqueio=false).

## Cenários de prova do roadmap — todos executados ao vivo

| Cenário (roadmap S9 / contratos BR-7.x) | Prova no E2E |
| --- | --- |
| Fornecimento acima do disponível do item bloqueado | item 18 un: fA=7 (disp 6) / fB=3 (disp 2) / fC=1 (disp 0) → 409 `FONTECIMENTO_EXCEDE_ITEM_CONTRATO` nomeada; 12/4/2 aceitos em sequência (disponibilidade consumida passo a passo) |
| Idempotência do fornecimento por chave | replay K-C2S9-F1 mesmo conteúdo → 200 MESMO id (sem duplicar); mesma chave conteúdo divergente (11) → 409 `FORNECIMENTO_CHAVE_IDEMPOTENCIA_CONFLITO` |
| Rejeição exige motivo + prazo paramétrico | rejeição sem motivo → 400 `RECEBIMENTO_REJEICAO_SEM_MOTIVO`; com motivo → prazo de substituição **2026-11-19** (2026-11-12 + 5 dias úteis do parâmetro do banco, sábado/domingo pulados) gravado no banco |
| Fornecimento só fecha com tudo recebido E desfechos registrados | recebido total mas rejeição sem desfecho → status `PARCIALMENTE_RECEBIDO` (não fecha); desfecho SUBSTITUIDO → `CONCLUIDO`; 2º desfecho → 409 `REJEICAO_DESFECO_JA_REGISTRADO`; sem rejeição → fechamento automático (`CONCLUIDO` de f2/f3) |
| Fatura divergente não entra | CNPJ ≠ fornecedor derivado → 400 `FATURA_FORNECEDOR_DISCREPANTE`; 1.199 vs 1.200 do pedido → 400 `FATURA_VALOR_DIVERGENTE_DO_PEDIDO`; natureza XML → 400 `FATURA_NATUREZA_INVALIDA`; NF-e 1.200 → 200; replay → mesmo id; 2ª NF diferente → 409 `FATURA_JA_VINCULADA_CONFLITO` |
| Obrigância única, valor somente aprovado, liquidação com fatura, estorno por compensação | OB-C2S9-F1 = **1.000,00** (6×100 — as 2 rejeitadas FORA) `REGISTRADA`; replay mesma referência → mesmo id; outra referência para o mesmo fornecimento → 409 `OBRIGACAO_DUPLICADA_REFERENCIA`; antecipada (fornecimento incompleto) → 409 `OBRIGACAO_FORNECIMENTO_INCOMPLETO`; liquidar → `LIQUIDADA` + data; de novo → 409 `OBRIGACAO_JA_LIQUIDADA`; OB-C2S9-F2 liquidar SEM fatura → 409 `OBRIGACAO_LIQUIDACAO_SEM_FATURA` → estorno com justificativa → `ESTORNADA` (terminal, compensação) → liquidar de novo → 409 |
| Destinação estoque = 1 movimento referenciado / patrimônio = ficha no ato | estoque 2 un de r1 → 1 `almoxarifado_movimentacao` ENTRADA/FORNECIMENTO `0.000→2.000` `FORN-{f1}-REC-{r1}`; replay mesma referência/conteúdo → mesmo id e movimento PERMANECE 1; mesma referência conteúdo divergente (5) → 409 `DESTINO_REFERENCIA_CONFLITO`; patrimônio 2 un (EQUIPAMENTO) → `patrimonio_bem` no ato com `PAT-FOR-{destId}`, valor 200,00, ATIVO, contrato/NF/data do recebimento/localização |
| Bloqueios de destino nomeados | local inativo → 409 `DESTINO_LOCAL_INATIVO`; local inexistente → 404 `DestinoLocalInexistente`; destino além do aprovado (4+3>6) → 409 `DESTINO_EXCEDE_QTD_APROVADA` |
| Execução contratual S8 continua consistente após a frente S9 | espelho legado `REC-FOR-*` alimenta a leitura de execução: totalFornecido **1.600,00**, pctExecutado **88,89**, bloqueiaNovoFornecimento=false — a S9 consome e mantém o sinal da S8 |

## Defeitos encontrados NA PRÓPRIA prova (sem máscara)

1. **Binário Release obsoleto na rodada r1** — a API estava servindo `Sigov.Api.dll`/
   `Sigov.Infrastructure.dll` compiladas às 10:41, enquanto o controller/repositório foram
   finalizados às 14:57/16:52: as 9 rotas novas simplesmente não existiam no runtime e os 16
   checks da frente caíram em **routing 404** ("Request reached the end of the middleware
   pipeline without being handled by application code" no log da API), enquanto os 382 checks de
   base/S8 PASSaram — o que isolou o problema da aplicação em si. O "build 0w/0e" anterior havia
   saído para outro diretório/configuração. Remediado: rebuild `dotnet build Sigov.sln -c
   Release -warnaserror` (0w/0e) + reinício dos apps + validação do output efetivo
   (`src\Sigov.Api\bin\Release\net10.0`) ANTES da rodada seguinte. Regra local registrada:
   antes da corrida oficial, conferir que o binário executado contém as novas superfícies
   (sonda de rota/marcador), não apenas que o build passou.
2. **Queda dos apps entre a sonda e o lançamento (rodada r2)** — Web e API no ar na
   double-probe (18:14, WEB 200/API 401) e fora do ar no início da rodada (18:21, HTTP 000 em
   todos os env checks; driver abortou cedo por login impossível). Mesmo padrão crônico da S8
   (RAM baixa do host). Reiniciado com **double-probe espaçada + medição de RAM livre** antes de
   qualquer lançamento (18:26: RAM 536 MB → apps no ar → 1.473 MB livres, estável nas duas
   sondas). Ambiente, não artefato.
3. **SQL inválida na leitura do check de desfecho do driver (rodada r3)** — o SELECT de
   verificação usava `... from f where f.id=X cross join r where r.id=Y` (segundo WHERE após
   join) → `syntax error at or near "cross"` no psql; a API tinha retornado 200 e o estado do
   banco estava correto — caiu só a leitura. Corrigido no driver para join clássico
   (`from f, r where f.id=X and r.id=Y`).
4. **Coluna `is_deleted` inexistente em `almoxarifado_material` (rodada r3)** — a conferência do
   material no destino ESTOQUE filtrava `and not is_deleted`, coluna que NÃO existe no schema
   legado (confirmado por `\d`: a tabela carrega apenas `ativo`) → `42703: column "is_deleted"
   does not exist` (500) em todas as 4 chamadas do check (destino válido, replay, conflito de
   referência e excede — todas morriam antes de chegar às regras seguintes). Corrigida a query
   (filtro apenas por `ativo`) e varridas as demais referências S9 a tabelas legadas
   (`almoxarifado_local`/`almoxarifado_movimentacao`/espelho legado — sem `is_deleted`, queries
   já limpas).
5. **Duplicidade de obrigação conferida só pela referência (rodada r3)** — gerar obrigação com
   referência diferente para o MESMO fornecimento CONCLUIDO inseria uma segunda linha em vez da
   nomeada `OBRIGACAO_DUPLICADA_REFERENCIA` (invariante BR-7.7: 1 fornecimento aprovado = 1
   obrigação). A busca de duplicidade era por `referencia_idempotencia` (tenant-wide) e não via
   fornecimentos já obrigacionados. Corrigido em código: busca por `fornecimento_id` (limit 1)
   — replay da MESMA referência com MESMO valor + `REGISTRADA` permanece idempotente (mesmo id);
   qualquer referência diferente para o mesmo fornecimento → 409 nomeada.
6. **`documento_origem` do movimento de estoque gravando o id do contrato (rodada r4)** — o
   documento referenciado `FORN-{f}-REC-{r}` saiu com o id do CONTRATO (`FORN-18-REC-5`) em vez
   do id do FORNECIMENTO (`f1=4` na rodada): a consulta do recebimento em
   `DestinacaoRegistrarAsync` projetava `f.contrato_id` e não `f.id`. O movimento em si estava
   correto (único, motivo/saldos/documento formatado certo) — desviava apenas a cadeia de
   referência, que deixaria de resolver diretamente qual fornecimento gerou a entrada (exigindo
   passagem pela tabela intermediária). Corrigido em código: a tuple passa a projetar `f.id` e o
   INSERT usa `@F = rc.FornecimentoId` (o PATRIMONIO segue usando o contrato em `@C`, como
   projetado).
7. **Regra de teto avaliada antes da idempotência por referência (rodada r5)** — com o documento
   corrigido, o check de destino expôs a ORDEM das regras: a checagem de excedente (destino total
   ≤ quantidade aprovada) corria ANTES da busca por referência. A chamada com a MESMA referência
   e conteúdo divergente (qtd 5 após 2 destinadas) caía em `DESTINO_EXCEDE_QTD_APROVADA`
   (5+2=7 > 6) em vez da esperada `DESTINO_REFERENCIA_CONFLITO`. Havia ainda um quebra latente de
   idempotência: um replay de uma destinação feita no limite do teto receberia 409 de excedente em
   vez de replay sem efeito (a soma já inclui a própria destinação). Corrigido em código com
   comentário explicativo: a busca por referência (replay/conflicto) agora precede a regra de
   teto, que vale apenas para referências novas.

## Verificação ao vivo (PG16 `p1_dev`)

- Migration aplicada no total 3× via psql `ON_ERROR_STOP=1` — exit 0; pós-condição
  `C2S9-POSTCOND` verde (5 tabelas, índices, colunas de relaxamento e domínios de status
  legados alargados);
- Fixture + cleanup SQL do bloco S9 pré-validados em dry-run dedicado (transação) antes da
  corrida — exit 0, ids resolvidos (entidade/centro de custo/usuário/grupo/locais/materiais) e
  contagem de resíduo zero pós-rodada;
- `scripts/generate-script-completop.ps1 -Verify`: "Scripts completos estão sincronizados com as
  migrations." (exit 0).

## Gates

- Build Release `-warnaserror` (`Sigov.sln`): **0w/0e** (na versão inicial e revalidado após
  cada correção em código: 4–5 antes da r4, 6 antes da r5, 7 antes da r6);
- Suítes: **851/851 verdes** (Unit 522 / Integration 171 / Api 158, 0 ignorados) sobre o
  binário final — incluindo o fact de manifest com 236 segmentos;
- TIER2 E2E HTTP (`scripts/c1_tier2_e2e.ps1` sobre apps reais Web :5099 / Api :5100/:5001):
  **403/403 PASS na corrida oficial r6** (base 382 revalidada + 21 checks novos da frente S9),
  `fixture.removido restantes=0` (auto-limpeza do bloco S9 em cascata + contagem C2S9-% nos
  globais); histórico das rodadas (sem máscara): **r1 = 367/16** (binário obsoleto — defeito 1;
  base integral 382 PASS + auto-limpeza S9 verde), r2 = abortada cedo por queda dos apps no
  ambiente (defeito 2 — env 000 na abertura; sem artefato em causa), **r3 = 399/4** (defeitos
  3–5: SQL do driver, `is_deleted` do material e duplicidade por fornecimento da obrigação —
  base integral PASS novamente), **r4 = 402/1** (defeito 6: `documento_origem` do movimento com
  o id do contrato em vez do fornecimento — todos os demais checks da frente S9 PASS, base
  integral PASS), **r5 = 402/1** (defeito 7: regra de teto avaliada antes da idempotência por
  referência na destinação), **r6 = 403/403 PASS** com `fixture.removido restantes=0`
  (corrida oficial — a rodada só foi reavaliada com o processo de fato morto e o log
  estabilizado);
- Artefatos `artifacts/c1_tier2_e2e/c1-tier2-e2e-result.json` / `c1-tier2-e2e-report.md`;
- JSON alterado (`manifest.json`) revalidado (parse + generator `-Verify` exit 0); PS driver
  validado pelo parser após cada edição (regra 19); RC própria desta sprint (regra 20).

## Notas de ambiente e resíduos declarados (regra 13 — nada mascarado)

- Sem view web nesta fatia (contrato da S7 = API + serviço sobre o schema; a interface web da
  frente administrativa entra depois) → sem jornada de navegador/screenshots; browser segue
  **BLOCKED** herdado da S6 (regra 18).
- Herdado das sprints anteriores (sem alteração nesta): RAM crônica baixa do host — 7º evento de
  queda de apps documentado (r2 desta sprint; os 6 anteriores na S8); mitigação padrão = double-
  probe de readiness + medição de RAM antes de todo lançamento; container `sigov-api`
  Restarting(139) legado contornado via API local, `rc50_25` órfã, generator "excluídas 4",
  `p1_dev.schema_migrations` sem ledger das entradas recentes (padrão EVO: psql direta), commit
  message ASCII via `-F`.
- A família legacy permanece ativa e compatível — nenhum delete/convert destrutivo (regra 5):
  o espelho S9 ADICIONA linhas em `compras_recebimento(_item)` com documento `REC-FOR-*` e os
  domínios de status do legado foram apenas ALARGADOS; as chaves de autorização usadas são
  exatamente as canônicas pontilhadas da seed S7 + reuso documentado dos módulos destino
  (banco = autoridade, regra 11).
- Valores (prazo de substituição, teto de execução 100%) vêm de `parametro_modulo` com override
  por tenant — nada hardcoded em C# (regras 11/24).

## Jornada da fatia e gates

- **Jornada da fatia: DEMANDA aprovada (entidade federal C2S9) → PROCESSO ÚNICO → DISPENSA com
  base legal do catálogo → CONTRATO VIGENTE (assinatura física, 1.800,00 em 18 un × 100,00) →
  FORNECIMENTOS f1=12/f2=4/f3=2 (valor e fornecedor DERIVADOS; disponibilidade conferida) →
  RECEBIMENTO PARCIAL c/ rejeição motivada (prazo paramétrico 2026-11-19) + ESPELHO LEGADO
  REC-FOR-* → DESFECHO SUBSTITUIDO → CONCLUIDO → FATURA NF-C2S9-001 CONFERIDA (CNPJ/valor/
  natureza/idempotência) → OBRIGAÇÃO OB-C2S9-F1 = 1.000,00 (só o aprovado) LIQUIDADA +
  OB-C2S9-F2 = 400,00 ESTORNADA POR COMPENSAÇÃO → DESTINAÇÃO ESTOQUE (movimento único
  FORN-{f}-REC-{r}) + PATRIMÔNIO (ficha PAT-FOR no ato) com bloqueios nomeados → EXECUÇÃO
  CONTRATUAL COERENTE (1.600,00 = 88,89%, sem bloqueio) — tudo provado por HTTP real com falhas
  nomeadas no banco PostgreSQL.**
- Gates: migration idempotente 3× + `C2S9-POSTCOND` verde; manifest **235 entradas** +
  consolidados sincronizados/verificados; build Release `-warnaserror` **0w/0e**; suítes
  **851/851**; TIER2 E2E HTTP **403/403** com `restantes=0`; cenários canônicos do roadmap
  S9 executados ao vivo; nada municipal hardcoded (entidade **federal** de esfera resolvida do
  banco — regra 22).
- Próximo exato: **Ciclo 2 Sprint 10 — etapa 3/3 da frente de Compras (BR-7.8 contrapartidas
  convênio/obra + dependências jurídicas)**. GED por último.
