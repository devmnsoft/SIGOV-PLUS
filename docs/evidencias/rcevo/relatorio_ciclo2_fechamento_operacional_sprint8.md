# Relatório Ciclo 2 · Sprint 8 — Compras integradas (1/2): demanda → contratação → contrato

Data: 2026-10-13 · Trilha: commit `52e1f4a1` (Ciclo 2 S7) + commit desta sprint (migration/manifest/consolidados/C#/suítes/E2E/docs) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

**Implementação integral dos contratos BR-7.x da S7 para a etapa 1/2 da frente administrativa**:
demanda tipada (BR-8.1) → processo único (BR-8.2) → procedimento/habilitação/lances com zona
ME-EPP (BR-8.3) → julgamento/adjudicação/homologação + rejeição de inexequível (BR-8.4) →
contrato nascendo da homologação com execução/prorrogação/rescisão (BR-8.5) → cadeia direta
de dispensa com base legal do catálogo art. 75 (BR-8.6), sobre os contratos canônicos definidos
em `docs/CONTRATOS-FRENTE-ADMINISTRATIVA-C2S7-MULTIESFERA.md` (BR-8.x do roadmap
`docs/roadmap/ciclo2_fechamento_operacional_21_sprints.md`). Regras aplicadas: 4–5 (PK bigint
identity; UUID legado intacto), 6–7 (migration idempotente + manifest + consolidados), 11/12
(banco como autoridade — modalidades, bases legais e parâmetros lidos do catálogo), 13 (falhas
nomeadas explícitas), 14 (estender suítes existentes — sem nova classe), 19 (sintaxe dos
artefatos alterados), 20 (RC própria) e 21–24 (multi-esfera: nada municipal hardcoded — esfera
da entidade institucional resolve gestora/executora; margem ME-EPP, tetos e prazos parametrizados
por banco).

**Fora do escopo (etapa 2/2, Sprint 9):** fornecimento contra item de contrato, recebimento por
item com rejeição/substituição, destinação estoque/patrimônio via comandos tipados e obrigação
financeira idempotente. Esta sprint deixa o sinal de teto (`bloqueiaNovoFornecimento`) pronto
para o consumo da S9.

## O que foi entregue

**1. Migration idempotente** — `database/postgres/migrations/20261013140000_evo_c2s8_compras_integradas_1_2.sql`
(aplicada **3×** via psql `ON_ERROR_STOP=1`, exit 0 — idempotência provada):
- 7 colunas novas em `compras_demanda` (urgência/dupla aprovação/esfera/unidades) sem conversão
  destrutiva;
- 5 tabelas novas com PK `bigint generated ... as identity` (regra 4): `compras_demanda_item`,
  `compras_lance` (com `chave_idempotencia` único por tenant×licitação×chave),
  `compras_adjudicacao_item`, `compras_contrato_item` e `compras_dispensa_base_legal`;
- 9 índices (apoio de unicidade/filtro por tenant);
- `compras_processo.demanda_id` (+ relaxamento não-destrutivo de modalidade/critério para
  procedimentos de dispensa sem licitação prévia); `compras_fornecedor.categoria_me_epp`;
  `compras_licitacao.zona_me_epp_status/zona_motivo`; `compras_adjudicacao.valor_total`;
  `compras_contrato.homologacao_id/data_eficacia/assinatura_modo` + índice único de contrato
  vigente por homologação (backstop de duplicidade);
- catálogo: modalidades do banco ampliadas para **10 por tenant** (8 da Lei 14.133 + `DISPENSA`
  e `INEXIGIBILIDADE` como modalidades diretas — 90 linhas × 9 tenants, sem alterar a seed
  original, apenas relaxamento + insert `where not exists`);
- **5 bases legais de dispensa** (art. 75 I/II/III/VI/XI Lei 14.133) em catálogo próprio — a
  contratação direta nunca usa texto livre (BR-8.6);
- **8 parâmetros** no módulo `COMPRAS` de `parametro_modulo` (ordens 220–227): margem ME-EPP
  5%, prazo máximo de homologação 30 dias, teto consulta prévia 118704,36 (art. 75 II vigente),
  teto de execução 100%, teto/prorrogação art. 106 (anos e máximo de vezes) etc. — tudo com
  override por tenant via `parametro_modulo_valor` (mesmo padrão da Educação C2S5);
- pós-condição explícita `C2S8-POSTCOND` (regra 13): falha nomeada se faltar qualquer tabela/
  índice/parâmetro/base legal/modalidade.

**2. Manifest + consolidados** — entry `20261013140000` (checksum SHA-256 atualizado após o delta
final das modalidades: `8e0ca18a…bbacb`) → manifest **234 entradas**; consolidados
(`script_completo.sql`, `script_completo_dev.sql`, `database/script_completo.sql`,
`script_completop.sql`) regenerados pelo gerador oficial e **verificados**
(`scripts/generate-script-completop.ps1 -IncludeDevelopmentSeed` + `-Verify` — exit 0; "excluídas
4" herdado, não-fault).

**3. C# (3 novas classes de serviço/API — regra 14 se aplica à famílias de teste, estendidas sem
nova classe)**:
- `src/Sigov.Application/Compras/Integradas/ComprasIntegradasContracts.cs` — contexto
  institucional, requests, DTOs, `IComprasIntegradasService` e **~49 exceções nomeadas**
  seguindo os nomes canônicos do contrato da S7 (`DEMANDA_ITEM_INVALIDO`,
  `DEMANDA_URGENCIA_SEM_DUPLA_APROVACAO`, `DEMANDA_APOS_APROVACAO_IMUTAVEL`,
  `PROCESSO_JA_VINCULADO_A_DEMANDA`, `SEPARACAO_FUNCOES_VIOLADA`,
  `PREFERENCIA_ME_EPP_NAO_DOCUMENTADA`, `HOMOLOGADOR_E_ADJUDICADOR_IGUAIS`,
  `CONTRATO_SEM_PROCEDIMENTO_HOMOLOGADO`, `PRORROGACAO_EXCEDE_TETO_ART_106`,
  `RESCISAO_SEM_ATO_JUSTIFICATIVA`, …) + `COMPRAS_INTEGRADAS_NAO_DISPONIVEL` (404, schema ausente);
- `src/Sigov.Infrastructure/Compras/ComprasIntegradasRepository.cs` — Dapper/PG16 (sem EF Core),
  transações por ato, guards de schema, ~19 comandos; regras de negócio íntegras: dupla
  aprovação de urgência por autoridade distinta, imutabilidade pós-aprovação, processo único na
  mesma transação (Lei 14.133 art. 128), abrir idempotente por mesma modalidade / 409 para outra,
  consulta prévia limitada pelo teto parametrizado, dispensa exige base legal do catálogo,
  lances decrescentes com 1 registro por autor×sequência + chave de idempotência, zona ME-EPP
  aberta pelo primeiro lancista ME/EPP, preferência calculando limite = melhor fora × (1 −
  margem/100) com desfecho documentado, julgamento derivando propostas dos melhores lances
  (zona ABERTA bloqueia; empate exige justificativa registrada), rejeição de proposta
  inexequível registrada entre classificação e adjudicação, adjudicação competitiva (menor ordem
  não rejeitada) ou direta (fornecedor indicado) com **valor derivado** da planilha
  (proporcional por peso com ajuste de arredondamento no último item), homologação com
  autoridade distinta do adjudicador e dentro do prazo parametrizado, contrato espelhando itens
  da adjudicação com valor derivado (sem edição livre), execução Σ recebimentos vs teto
  parametrizado com flag de bloqueio, prorrogação limitada por anos e vezes (rastro append-only
  em `compras_contrato_historico`) e rescisão unilateral exigindo notificação prévia registrada
  (art. 129);
- `src/Sigov.Api/Controllers/ComprasIntegradasController.cs` — `[Route("api/compras/integradas")]`
  com **19 endpoints**, autorização fail-closed por **chave pontilhada da seed S7** no banco
  (regra 11) replicando o padrão legado `Run(chave,…)` do `ComprasController`; mapeamento 404
  (inexistente/fora do catálogo/schema ausente), 400 (entrada/regra simples), 409 (conflito);
- DI registrado em `Sigov.Infrastructure/DependencyInjection.cs`.
- **Contexto institucional multi-esfera (regras 21–24):** cada comando resolve `sigov.entidade`
  do usuário autenticado (tenant+ativa+!is_deleted); ausência de `esfera_governo` falha
  explicitamente (nunca assume municipal); unidade gestora/executora = coalesce(campos da
  entidade, própria); tudo escrito com a esfera resolvida.

**4. Suítes estendidas (regra 14)** — `SaasComercialTests` (classe existente): asserção do
manifest 234→235 segmentos + comentário de trilha da entrada C2S8. Resultado: **851/851 verdes**
(Unit 522 / Integration 171 / Api 158, 0 ignorados).

**5. TIER2 E2E HTTP real (driver `scripts/c1_tier2_e2e.ps1`)** — bloco **S8 com 48 checks**
(fixture fictício `C2S8-%` no tenant 910001 com auto-limpeza em cascata e contagem nos
`restantes` globais): jornada completa por HTTP real Web :5099 → ApiToken → bearer Api
:5100 → Dapper → PG16, com **usuário dedicado `c2s8.admin`** vinculado a **entidade fixture de
esfera estadual** (regras 21–24) copiando a cadeia `usuario_grupo/grupo/perfil` do admin
(autorização 100% resolvida pelo banco — prova viva da regra 11). Como o perfil dessa cadeia
carregava chaves da geração anterior da frente, o fixture também concede explicitamente as 15
chaves canônicas da S7 a esse perfil (vínculos marcados com sentinel `correlation_id`,
`DO NOTHING` e limpeza cirúrgica — sem tocar nos 25 vínculos pré-existentes; defeito 3 acima).

## Cenários de prova do roadmap — todos executados ao vivo

| Cenário (roadmap S8) | Prova no E2E |
| --- | --- |
| Demanda emergente sem dupla aprovação bloqueada | `aprovada_parcial` → `abrir` 409 `DEMANDA_URGENCIA_SEM_DUPLA_APROVACAO`; 2ª aprovação pela mesma autoridade 409 `SEPARACAO_FUNCOES_VIOLADA`; por autoridade distinta → `APROVADA` + processo único criado na transação |
| Zona ME-EPP respeitada | zona `ABERTA` no 1º lance do fornecedor ME; julgamento bloqueado 409 `PREFERENCIA_ME_EPP_NAO_DOCUMENTADA`; fornecedor não ME-EPP 409 `FORNECEDOR_FORA_DA_ZONA_ME_EPP`; preferencia **dentro da margem paramétrica 5%** (900,00 vs limite 902,50) → `ENCERRADA_POR_PREFERENCIA` documentada; repetida 409 `ZONA_ME_EPP_JA_RESOLVIDA`; o ME vence o julgamento (ordem 1) |
| Adjudicação sem homologação não gera contrato | contrato sem homologação 404 `HOMOLOGACAO_INEXISTENTE`; contrato nasce só da homologação id (vínculo `homologacao_id` + índice único vigente) |
| Contrato acima do teto de execução não aceita fornecimento | execução 100% do contratado (teto parametrizado 100%) → `bloqueiaNovoFornecimento=true` (sinal consumido pela S9) |
| Violações de separação de funções nomeadas | solicitante≠decisor, adjudicador≠homologador (`HOMOLOGADOR_E_ADJUDICADOR_IGUAIS` 409) — todas com falha nomeada no corpo |

Além dos 5 cenários canônicos: validações nomeadas de itens (qtd/unidade/preço/pesquisa/centro
de custo inativo), máquina da demanda (repelir/reabrir/cancelar com estados inválidos 409),
modalidade fora do catálogo 404, consulta prévia acima do teto 409, abertura idempotente +
processo único 409 para modalidade diferente, habilitação por tipo com reprovação exigindo
justificativa, lance não decrescente 409, idempotência de lance por chave (replay mesmo efeito;
conteúdo conflitante 409), julgamento repetido 409, rejeição de proposta inexequível registrada
+ repetida 409, adjudicação sem itens 400 / repetida 409, homologação repetida 409, contrato
duplicado 409, prorrogação acima do teto art. 106 409 (e dentro do teto 200 com rastro no
histórico), rescisão unilateral sem notificação 400 / com notificação 200 + rastro, contrato
resciso não prorroga 409, dispensa sem base legal 400 / código inventado 404 / com `ART_75_I`
200 (rastro no jsonb do processo único), adjudicação direta sem fornecedor 400 / com
fornecedor 200 (valor derivado R$ 500,00) e cadeia direta completa até contrato de assinatura
física — o ME preferenciado venceu a licitação competitiva e o contrato espelhado saiu com
valor derivado **R$ 900,00 = valor do lance vencedor** (sem edição livre).

## Defeitos encontrados NA PRÓPRIA prova (sem máscara)

1. **Fixture S8 vs constraints reais do banco** — a rodada 1 abortou no insert do fixture com o
   erro nomeado do Postgres: `entidade.cnpj` é NOT NULL (não coberto pela coluna usada) e
   `compras_fornecedor.tipo_pessoa` tem check constraint `FISICA|JURIDICA` (fixture usava `'J'`).
   Corrigido no próprio driver (CNPJ zerado fictício + `JURIDICA`) e o fixture inteiro
   pré-validado em transação `BEGIN/ROLLBACK` antes da rodada seguinte — o erro veio do schema
   legado, não do código da sprint (nenhuma linha de C#/migration envolvida).
2. **BOM do driver PS removido por ferramenta de edição** — a edição programática do driver
   gravou UTF-8 sem BOM; PowerShell 5.1 passou a parsear como ANSI e o parser reportou 507
   erros em strings acentuadas antigas. BOM restaurado byte-exact e parser revalidado (0 erros).
   Regra local registrada: edição de `.ps1` UTF-8-BOM deve preservar o BOM (regra 19).
3. **Autorização 403 em toda a frente (rodada 2)** — a cadeia copiada do admin
   (`grupo_acesso 910401`) resolve para o perfil `ADMIN_TENANT`, que carrega as chaves da
   geração **anterior** da frente (`compras.ata.*`, `compras.cotacao.*`,
   `compras.requisicao.*`, …) — e não as chaves canônicas da S7 que o controller consome
   (a seed da S7 as ligou aos perfis `SUPERADMIN`, cujas linhas pertencem a outros tenants do
   catálogo). A rodada 2 expôs isso com 403 nomeados em todos os endpoints da frente.
   Resolução no próprio fixture: concessão explícita das 15 chaves canônicas ao perfil da
   cadeia, com `correlation_id` sentinel (`cf58c2ab-…`), `DO NOTHING` (preserva vínculos
   pré-existentes) e **limpeza cirúrgica por sentinel** — provada em transação
   `BEGIN/ROLLBACK`: 13 vínculos novos (2 das 15 já existiam na cadeia e foram preservados),
   os 25 vínculos compartilhados do perfil intactos após o cleanup.
4. **Cleanup da S8 preso em FK + resíduo de rodada interrompida (rodada 2)** — o cleanup do
   bloco parou na FK `auditoria_evento → usuario` (sessões/eventos criados pelo login e pelas
   decisões de autorização referenciam o usuário de teste) e `ON_ERROR_STOP` abortou o resto,
   deixando 2 linhas (`c2s8.admin` + `C2S8-ENT`). Expandi o cleanup para deletar 15 tabelas
   escopadas pelo usuário antes do delete dele (`autorizacao_decisao_auditoria`,
   `auditoria_evento`, `identidade_sessao`, `sessao_usuario`, `sessao_contexto_usuario`,
   `contexto_operacional_auditoria` — col. `usuario_executor_id`,
   `usuario_contexto_global_log` — col. `usuario_global_id`, `historico_login`,
   `trilha_auditoria`, `token_mfa`, `senha_redefinicao_token`, `fila_assinatura`,
   preferências/filtros salvos) + os grants sentinel. Os ~51 FAILs de base da rodada 2 foram
   resíduo da rodada 1 interrompida (mesmo padrão documentado da S6 rodada 3) — limpos pelo
   cleanup integral da própria rodada 2 (só restaram as 2 linhas do meu fixture, agora cobertas
   pelo cleanup corrigido; a contagem global `restantes` ganhou o termo do sentinel).
5. **`updated_by` NOT NULL ausente em INSERTs do repositório (rodada 4)** — o `POST /demandas`
   retornou 500 com a falha nomeada do Postgres visível no log da API
   (`23502: null value in column "updated_by" ... compras_demanda`); a mesma lacuna existia no
   INSERT de `compras_licitacao` (só não apareceu porque a cadeia morria antes no `abrir`).
   Corrigido em código (os dois INSERTs passam a gravar `updated_by=@U`) e fechado com auditoria
   automática de **todos** os `insert into sigov.compras_*` do repositório contra as colunas
   NOT NULL sem default do schema real — resultado: apenas essas duas ocorrências (as demais
   "ausências" da varredura são PKs `generated as identity`). Build Release `-warnaserror` 0w/0e
   após a correção.
6. **Domínio de enum legado na coluna compartilhada (rodadas 4–5)** — com o insert passando no
   ponto anterior, a criação da demanda caiu na falha nomeada
   `23514: violates check constraint "compras_demanda_prioridade_check"` (rodada 5): o check
   LEGADO da tabela aceita apenas `baixa|normal|alta|urgente` (minúsculas) enquanto o comando
   tipado da frente integrada grava o domínio canônico da S8 (maiúsculas). A auditoria
   preventiva de **todas** as checks da tabela (antes da rodada 6) revelou o choque idêntico no
   `compras_demanda_status_check` legado (`rascunho|submetida|aprovada|reprovada|cancelada`),
   que bloquearia `SOLICITADA`/`APROVADA_PARCIAL`/`APROVADA`/`REPELIDA`/`CANCELADA`. Correção
   **na própria migration S8** (ainda não publicada): os dois checks foram relaxados
   idempotentemente (`drop if exists` + `add`) para aceitar as duas gerações — legada
   (minúscula) e integrada S8 (maiúscula) — sem tocar em dado legado existente (mesmo padrão
   já usado na seção 12 da migration sobre `compras_modalidade_config`); migration re-aplicada
   **3×** com POSTCOND reforçado (exige agora a presença das checks relaxadas), checksum do
   manifest atualizado (`8e0ca18a…` → `f20ff8d9…`) e consolidados regenerados + `-Verify` verde. A mesma auditoria preventiva, estendida a
    **todas** as tabelas legadas tocadas pela frente antes da rodada 7, achou os mesmos choques em
    mais 6 checks (status de `licitacao`, `processo`, `habilitacao`, `proposta` e `contrato` + o XOR
    de vinculo `lote_id`/`item_id` da proposta, que a proposta S8 de nivel fornecedor nao satisfaz)
    — todos relaxados no mesmo padrao das duas geracoes na mesma migration (secao 1c), POSTCOND
    reforcado para exigir cada check relaxado e migration re-aplicada 3x verde.
7. **Driver E2E vs shape da resposta + fixture de recebimento incompleto (rodada 6)** — com o
   schema liberado, o `POST /demandas` voltou 200 e o driver abortou num cast: a API devolve o id
   criado como objeto `{ "id": n }` (padrao legado `new { id }` dos controllers) e o driver
   esperava um `long` puro em 8 pontos (criacoes de demanda x4, lances x2, homologacoes x2) —
   corrigido no driver para ler `data.id`. A auditoria preventiva seguinte achou ainda no fixture
   de recebimento: (i) status `'CONFIRMADO'` fora do dominio legado
   (`PENDENTE|INTEGRADO|CANCELADO`) — trocado por `PENDENTE` (a leitura de execucao da BR-8.5 soma
   os itens sem filtrar por status); (ii) item de recebimento sem as colunas NOT NULL
   `processo_item_id` (FK `compras_processo_item`) e `tipo_material` (`CONSUMO|PERMANENTE`) — o
   fixture agora cria um `processo_item` com marcador `ITEM-C2S8-1` idempotente antes do item; o
   cleanup do bloco ganhou o delete de `compras_processo_item` e a contagem global de residuos o
   termo correspondente.
8. **Materialização Dapper de `DemRow` + transação já commitada (rodadas 5–7)** — com o schema
   liberando a criação, todo caminho que termina em leitura do DTO (aprovar/repelir/cancelar/
   reabrir) devolveu 409 genérico: o construtor do record tinha parâmetro `DateTimeOffset` e o
   Npgsql traz `timestamptz` como `DateTime(Utc)`; o bind posicional do Dapper exige assinatura
   exata (e também não aceita `Nullable<Int64>` para coluna `Int64` do grid, nem colunas sem alias
   no casamento por nome — `valor_estimado`). Corrigido em código: `DemRow` com `long` não nulos +
   `coalesce(...,0)`, `DateTime` e aliases alinhados ao parâmetro, tuple da homologação com
   `DateTime`; e as três leituras `ToDtoAsync` após `CommitAsync` passaram a rodar sem transação
   explícita (usar `NpgsqlTransaction` depois do commit lança "Transaction is already completed").
   Validação dedicada (probe) antes da rodada 8: criar → aprovar não urgente = `APROVADA` com
   processo único gerado e aprovar urgente 1ª vez = `APROVADA_PARCIAL`, cleanup integral verde.
    Mesma classe varrida no mesmo ciclo nas demais materializações por construtor da frente:
    `PropostaRejeitarAsync` (record público `PropostaIntegradaDto` trocado por tuple nomeada no
    padrão de `ContextoEntidadeAsync`) e a lista `DemandasAsync` (record interno + projeção para o
    DTO público), build 0w/0e.

9. **FK do cleanup homologacao → adjudicacao (rodada 8)** — com dados reais da cadeia completa
   pela primeira vez, o cleanup do bloco falhou: `compras_homologacao` não tinha delete próprio e
   o `delete from sigov.compras_adjudicacao` violava a FK `homologacao.adjudicacao_id`, parando a
   cascata no `ON_ERROR_STOP` e deixando resíduo (inclusive do fixture da base). Corrigido no
   driver: delete de `compras_homologacao` antes dos deletes de adjudicação + termo de contagem de
   resíduos para homologações órfãs (sem licitação); utilitário de limpeza manual idem (validado
   EXIT=0 restantes=0 sobre o resíduo da r8).
10. **Aritmética dos lances do driver vs. leilão decrescente (rodada 8)** — os lances de
    idempotência (F3 @ 1100/1099) e o lance da zona ME (F2 @ 980) vinham DEPOIS do melhor lance
    vigente (F1 @ 950), sendo rejeitados corretamente pela nomeada `LANCE_NAO_DECRESCENTE` (409)
    que o repositório exige em pregão, e derrubaram em cascata zona, julgamento bloqueado,
    classificação, adjudicação (vencedor errado), contrato (valor 950 ≠ 900), execução
    (pct 94,74) e prórrogações. Corrigidos os valores do fixture no driver sem alterar a semântica
    dos checks: sequência K = 949/948 (< 950), zona ME @ 930, lance final ME @ 900 dentro da
    margem (melhor fora zona 949 × 0,95 = 901,55 ≥ 900) e classificação
    `1:ME:900,2:F3:949,3:F1:950`.
11. **Data da prorrogação em cultura do host (rodada 8)** — `ContratoProrrogarAsync` interpolava o
    `DateOnly` no rastro histórico sem formato explícito; com o container da API em pt-BR escreveu
    `PRORROGACAO ate 01/11/2031` e o check esperava ISO `2031-11-01`. Corrigido em código:
    `ToString("yyyy-MM-dd", InvariantCulture)` — comportamento independente da cultura do host.
    (O check de urgência parcial da r8 segue sob investigação: evidência instrumentada no driver
    para a r9 isolar o operando exato — resolvido na r9: o dado do banco estava correto o tempo
    todo; o esperado continha erro de digitação no driver, tratado como defeito 13.)
12. **Coluna `zona_me_epp_status` menor que os estados canônicos S8 (rodada 9)** — a migration
    criou a coluna com `varchar(20)`, mas `ENCERRADA_POR_PREFERENCIA` tem 23 caracteres e
    `ENCERRADA_FORA_MARGEM` 21; a documentação do desfecho da preferência (único caminho que
    grava esses valores) estourou a coluna com `22001 value too long for type character
    varying(20)` e derrubou em cascata julgamento, rejeição, adjudicação, homologação, contrato,
    execução, prorrogação e rescisão. Corrigido na própria migration S8 (mesma RC, pré-commit):
    `add column ... varchar(40)` + `alter column ... type varchar(40)` idempotente para bases já
    criadas; checksum do manifest recomputado (`304e9d92…`), consolidados regenerados e `-Verify`
    verde; migration re-aplicada 3× exit 0 (9× no total nesta sprint).
13. **Erro de digitação no esperado do check de urgência (rodadas 8–9)** — o padrão do
    `demanda.urgencia-primeira-aprovacao-parcial` esperava `APROVADA_PARCIL|…` (faltava um "A")
    enquanto o banco gravava corretamente `APROVADA_PARCIAL`; o check nunca casaria mesmo com o
    HTTP 200 correto. Isolado por dump de codepoints em probe dedicado (texto 24 chars vs padrão
    23 chars); corrigido no driver (expectativa + variável de diagnóstico). Sem defeito de
    aplicação envolvido — o estado do banco estava certo desde a r8.
14. **Coluna `compras_proposta.status` menor que o estado canônico S8 (rodada 13)** — a rodada
    13 levou a rejeição por inexequibilidade pela primeira vez até o UPDATE real (com proposta
    de fato existente, após o fix da classificação) e expôs o limite legado `varchar(20)` da
    coluna: `REJEITADA_INEXEQUIVEL` tem 21 caracteres e virou `22001 value too long for type
    character varying(20)` (500 nas duas chamadas do check). Mesma classe do defeito 12;
    corrigido na própria migration S8 (mesma RC, pré-commit): `alter column status type
    varchar(40)` idempotente logo após o relaxamento do check de status; checksum do manifest
    recomputado (`d5472331…`), consolidados regenerados e `-Verify` verde; migration re-aplicada
    3× exit 0 (12× no total nesta sprint).

## Verificação ao vivo (PG16 `p1_dev`)

- Migration aplicada no total 12× (3× versão original + 3× pós-seção 1c + 3× pós-alargamento da
   zona + 3× pós-alargamento do status da proposta) via psql `ON_ERROR_STOP=1` — exit 0; pós-condição
  `C2S8-POSTCOND` verde (5 tabelas, 9 índices, 8 parâmetros, 5 bases legais art. 75,
  90 linhas de modalidade 10×9 tenants);
- Fixture S8 pré-validado em transação (exit 0, ids resolvidos) antes da corrida oficial;
- `scripts/generate-script-completop.ps1 -Verify`: "Scripts completos estão sincronizados com as
  migrations." (exit 0).

## Gates

- Build Release `-warnaserror` (`Sigov.sln`): **0w/0e**;
- Suítes: **851/851 verdes** (Unit 522 / Integration 171 / Api 158, 0 ignorados);
- TIER2 E2E HTTP (`scripts/c1_tier2_e2e.ps1` sobre apps reais Web :5099 / Api :5100/:5001):
  **382/382 PASS** (base 334 revalidada + 48 checks novos da frente S8), `fixture.removido
  restantes=0` (auto-limpeza do bloco S8 em cascata + contagem C2S8-% nos globais); histórico das
  rodadas (sem máscara): r1 = abortada no insert do fixture (defeito 1), r2 = 283/99 com 403 em toda a frente + cleanup preso em FK + resíduo da r1 (defeitos 3–4), r3 = abortada por queda da API (~90 s após o início; ambiente, não artefato), r4 = 338/44 com 500 no POST /demandas (defeito 5, corrigido em código) + queda da API no meio do bloco S8 (ambiente) + auto-limpeza integral verde pela primeira vez, r5 = 341/41 com o check legado de enum da demanda violado (defeito 6, corrigido em schema na própria migration S8 antes da rodada seguinte), r6 = 335 PASS / 0 FAIL registrados mas abortada num cast do driver apos o schema liberar a criacao (defeito 7, corrigido no driver) com residuo limpo manualmente antes da proxima rodada, r7 = 288/94 com colisões na base (resíduo da r6 abortada — 23505 em constraints de unicidade educacao/portal) + cascata integral do defeito 8 na cadeia S8, auto-limpeza integral verde de novo, r8 = 367/15 após o fix do defeito 8 (defeitos 9–11: cascata de aritmética de lances na cadeia S8 + FK do cleanup + data da prorrogação; urgência parcial instrumentada), r9 = 365/17 com a coluna da zona estourando no desfecho preferencial (defeito 12) em cascata até a rescisão + typo do esperado de urgência (defeito 13, isolado por codepoints), r10 = 207/175 com queda dos apps no meio da base (6º evento de ambiente — HTTP 0 em tudo a partir da seção merenda/estoque; o driver terminou e a auto-limpeza foi integral verde apesar da queda), r11 = 290/0 na base integral com parada silenciosa do driver no início de E19.E, antes mesmo do bloco S8 (7º evento de ambiente — host esgotando memória, ~680 MB livres; apps no ar e DB limpa ao final), r12 em execução plena quando a r13 foi lançada sobre ela — a notificação de fim de background do harness chegou prematura e o '33/0' então registrado era um snapshot intermediário, não o fim da rodada (mitigação aplicada antes da r13: shutdown dos servidores MSBuild/vstest + morte dos dotnets residuais para liberar RAM), r13 = 327/55 com 54 falhas de base causadas por CORRIDA entre dois drivers simultâneos — a notificação de fim de background do harness chegou prematuramente e a r13 foi lançada com a r12 ainda viva escrevendo no mesmo banco (23505 de unicidade, chaves de idempotência 409, subqueries ambíguas; nenhum defeito de aplicação) + o defeito 14 no rejeitar por inexequibilidade — única falha do bloco S8, cuja cadeia inteira ficou verde pela primeira vez (urgência, zona, margem, julgamento, homologação, contrato, execução, prorrogação e rescisão); desde então as rodadas só são reavaliadas com o processo de fato morto e o log estabilizado; r14 = 382/382 PASS oficial com auto-limpeza integral verde (fixture.removido restantes=0) após o fix do defeito 14;
- Artefatos `artifacts/c1_tier2_e2e/c1-tier2-e2e-result.json` / `c1-tier2-e2e-report.md`;
- JSON alterado (`manifest.json`) revalidado (parse + generator `-Verify` exit 0); SQL
  idempotente aplicada 3×; PS driver validado pelo parser (regra 19); RC própria desta sprint
  (regra 20).

## Notas de ambiente e resíduos declarados (regra 13 — nada mascarado)

- Sem view web nesta fatia (contrato da S7 = API + serviço sobre o schema `compras_*` existente;
  interface web da frente administrativa vem com a etapa 2/2+) → sem jornada de navegador/
  screenshots; browser segue **BLOCKED** herdado da S6 (regra 18).
- Herdado das sprints anteriores (sem alteração nesta): RAM crônica baixa do host (apps em
  shells dedicados + sonda de readiness; os apps caíram **seis vezes** durante esta sprint —
  após a rodada 2, durante a rodada 3, no meio do bloco S8 da rodada 4, após a rodada 8 e no meio da base da rodada 10, com RAM livre caindo a ~1,3 GB — e foram relançados por `s6_apps.ps1`; a queda da r3 abortou o driver por `HttpRequestException` transport-level na seção de chamadas paralelas e a da r4 causou a seré de HTTP 0 no fim do bloco S8, comportamento já documentado do ambiente), container `sigov-api` Restarting(139) legado contornado via
  API local, `rc50_25` órfã, generator "excluídas 4", `p1_dev.schema_migrations` sem ledger das
  entradas recentes (padrão EVO: psql direta), commit message ASCII via `-F`.
- A família legacy UPPER_SNAKE permanece ativa e compatível — nenhuma migração destrutiva
  (regra 5); as chaves de autorização usadas pelo controller são exatamente as 25 canônicas
  pontilhadas da seed S7 (banco = autoridade, regra 11).
- Valores de parâmetros (margem 5%, tetos, prazos) são defaults de homologação em
  `parametro_modulo` com override por tenant — nada hardcoded em C# (regra 24).

## Jornada da fatia e gates

- **Jornada da fatia: DEMANDA (tipada/urgência dupla aprovação/imutável) → PROCESSO ÚNICO →
  PROCEDIMENTO (modalidade do catálogo/teto consulta prévia/dispensa art. 75) → HABILITAÇÃO POR
  TIPO → LANCES (decrescentes/idempotentes/zona ME-EPP + preferência LC 123 dentro da margem) →
  JULGAMENTO (classificação automática, rejeição de inexequível) → ADJUDICAÇÃO (valor derivado
  — o ME preferenciado venceu) → HOMOLOGAÇÃO (autoridade distinta, prazo paramétrico) → CONTRATO
  (nasce da homologação, valor derivado R$ 900,00 = lance vencedor, GED/física, execução com
  teto, prorrogação art. 106 paramétrica, rescisão com ato) + cadeia direta DISPENSA → contrato
  completo — tudo provado por HTTP real com falhas nomeadas no banco PostgreSQL.**
- Gates: migration idempotente 3× + `C2S8-POSTCOND` verde; manifest **234 entradas** +
  consolidados sincronizados/verificados; build Release `-warnaserror` **0w/0e**; suítes
  **851/851**; TIER2 E2E HTTP **382/382** com `restantes=0`; 5 cenários canônicos do roadmap
  S8 executados ao vivo; nada municipal hardcoded (entidade estadual de esfera resolvida do
  banco).
- Próximo exato: **Ciclo 2 Sprint 9 — Compras integradas (2/2): fornecimento → recebimento →
  estoque/patrimônio → obrigação financeira** (BR-9.x do roadmap, consumindo o sinal
  `bloqueiaNovoFornecimento` desta sprint). GED por último.
