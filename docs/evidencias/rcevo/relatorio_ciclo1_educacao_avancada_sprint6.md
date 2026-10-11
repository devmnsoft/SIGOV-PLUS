# Relatório Ciclo 1 · Sprint 6 — Educação Avançada (merenda operação: estoque + distribuição de refeição com consumo transacional)

Data: 2026-10-11 · Commit validado: ver abaixo (trilha no commit desta sprint) · Banco: `p1_dev` no container oficial `sigov-postgres` (PostgreSQL 16.14)

## Escopo desta sprint

A **operação** de merenda vertical e multi-esfera (regra 24) sobre o cadastro base fechado na Sprint 5: dois comandos tipados novos que ligam cardápio → distribuição → movimento → estoque, tudo atômico:

- **Comando tipado `POST /api/educacao/merenda/estoques`** com `{produtoId, saldoInicial}`:
  - Produto deve existir e estar ativo em `sigov.educacao_merenda_produto`; ausência → **404 `MERENDA_PRODUTO_INEXISTENTE`** (não 500 — regra 13).
  - Sem linha de estoque para o produto: **cria** a linha em `sigov.educacao_merenda_estoque` espelhando `nome`/`unidade` do produto e abrindo com `saldo = saldoInicial`.
  - Linha já existente: **soma ajuste** ao saldo atual (`saldo += saldoInicial`) — nunca duplica linha.
- **Comando tipado `POST /api/educacao/merenda/distribuicoes/confirmar`** com `{cardapioId, data, itens:[{produtoId, quantidade}]}` — confirmação da distribuição da refeição:
  - Itens válidos antes de qualquer efeito: quantidade > 0, sem produto repetido; produto fora do menu do cardápio → **400 `PRODUTO_FORA_DO_MENU`**.
  - Cardápio deve existir (**404 `DISTRIBUICAO_CARDAPIO_INEXISTENTE`**), pertencer ao escopo institucional do contexto (**409 `CARDAPIO_FORA_DO_ESCOPO`**) e estar **aprovação vigente** (`ATIVA`/`ATIVO`; rascunho → **409 `CARDAPIO_NAO_APROVADO`**).
  - `data` dentro do período do cardápio (`data_inicio`..`data_fim`) → senão **409 `DATA_FORA_DO_PERIODO`**.
  - **Uma distribuição vigente por cardápio × data**: repetição → **409 `DISTRIBUICAO_DIA_EXISTENTE`**, com índice único parcial como backstop transacional.
  - **Estoque suficiente por item** antes de gravar → senão **409 `ESTOQUE_INSUFICIENTE`**.
  - **Persistência transacional única**: `FOR UPDATE` nas linhas de estoque em ordem determinística (`ORDER BY id`) → insert da distribuição (`ATIVA`, `competencia = data`) → itens espelhando produto → **movimentos `SAIDA` (−quantidade)** referenciando a distribuição → baixa do saldo (`saldo -= quantidade`). Qualquer falha rollback total (sem sucesso parcial simulado — regra 13).
  - A rota usa sub-path `/confirmar` porque a família avançada já expõe POST genérico em `/distribuicoes` (conflito de rotas ASP0023 sob `-warnaserror`).

## Arquivos alterados

| Arquivo | Mudança |
|---|---|
| `database/postgres/migrations/20261011150000_evo_c1s6_merenda_distribuicao_consumo_estoque.sql` | novo: índice único parcial `ux_merenda_distribuicao_tenant_cardapio_data` sobre `(tenant_id, referencia_id, competencia)` com `is_deleted=false and status not in ('ENCERRADA','ENCERRADO','CANCELADA','CANCELADO')` + pós-condição explícita (falha nomeada `S6-POSTCOND` se faltar o índice) |
| `database/postgres/migrations/manifest.json` | entrada `20261011150000` registrada (sha256 `29b2c670e4d34b9eabcbedc8f6bda4aada4af6406ba6382a4bc2ed8c7afff2ff`); 224 migrations |
| `script_completo.sql`, `script_completo_dev.sql`, `script_completop.sql` + `database/script_completo.sql` + `database/postgres/{script_completo,script_completo_dev}.sql` | regenerados (regra 7) via `generate-script-completop.ps1` (+`-IncludeDevelopmentSeed` para os dev) → S6 presente nos 6 consolidados (220 incluídas/4 excluídas) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoAvancadaContracts.cs` | 3 records novos (`EducacaoMerendaRegistrarEstoqueRequest`, `EducacaoMerendaDistribuirItemRequest`, `EducacaoMerendaDistribuirRefeicaoRequest`) + 2 métodos em `IEducacaoMerendaService` (`RegistrarEstoqueAsync`, `DistribuirRefeicaoAsync`) |
| `src/Sigov.Application/Educacao/Avancada/EducacaoEscopoExceptions.cs` | 8 falhas nomeadas novas: `MERENDA_PRODUTO_INEXISTENTE` (404), `DISTRIBUICAO_CARDAPIO_INEXISTENTE` (404), `CARDAPIO_FORA_DO_ESCOPO` (409), `CARDAPIO_NAO_APROVADO` (409), `DATA_FORA_DO_PERIODO` (409), `DISTRIBUICAO_DIA_EXISTENTE` (409), `ESTOQUE_INSUFICIENTE` (409), `PRODUTO_FORA_DO_MENU` (400) |
| `src/Sigov.Infrastructure/Educacao/Avancada/EducacaoAvancadaRepository.cs` | `RegistrarEstoqueAsync` (cria espelhando produto OU soma ajuste sobre linha existente) e `DistribuirRefeicaoAsync` (validações → transação: checagem do dia ocupado → locks `FOR UPDATE` ordenados → inserts de distribuição/itens/movimentos → baixa de saldo); DTO privado de leitura do cardápio com aliases SQL explícitas |
| `src/Sigov.Api/Controllers/EducacaoAvancadaControllerBase.cs` | mapeamento HTTP das 8 novas exceções (404/404/409/409/409/409/409/400) nos catches existentes |
| `src/Sigov.Api/Controllers/EducacaoMerendaController.cs` | rotas `POST …/estoques` e `POST …/distribuicoes/confirmar` |
| `tests/Sigov.IntegrationTests/EducacaoModuleSmokeTests.cs` | fato estático novo `MerendaOperacao_Sprint6_DistribuicaoComConsumoEstoque_UnicidadeDia` (classe existente, regra 14) |
| `tests/Sigov.IntegrationTests/SaasComercialTests.cs` | count do manifest no teste existente atualizado para registrar a migration (classe existente, regra 14) |
| `scripts/c1_tier2_e2e.ps1` | bloco E10 (12 checks: fixture, produtos, estoque inicial/ajuste/inexistente, cardápio ATIVA, distribuição com consumo real, estoque insuficiente, dia ocupado, data fora do período, produto fora do menu, cardápio rascunho) + CX ampliado (movimentos, itens de distribuição, distribuições, estoques, itens de cardápio, cardápios, produtos C1S6-%, escola S6); robustez: `--max-time` no curl e captura de stderr do curl no `Invoke-Api` |

## Gates executados

- Build Release `-warnaserror` de `sigov.sln`: **0w/0e**.
- Migration: aplicada **duas vezes** em `p1_dev` (idempotência) e post-condition verdadeira
  (`S6-POSTCOND|true`); manifest + 6 consolidados sincronizados (regras 6–8).
- Tier-2 E2E HTTP ao vivo (`scripts/c1_tier2_e2e.ps1`): **74/74 PASS** (ts=2026-10-11T16:51:48Z),
  fixtures fictícios `c1e26-%`/`C1S5-%`/`C1S6-%` (tenant 910001) aplicados e removidos
  ao final (resíduo zero); os 62 checks das Sprints 1–5 (E0–E9) revalidados na mesma rodada.
- Suítes (execução limpa após build): Unit **521/521** · Integration **163/163** · Api **158/158**
  (0 ignorados; total 842; o delta de +1 na Integration é o fato estático novo da Sprint 6).

## Evidência Tier-2 ao vivo (bloco E10 — operação de merenda)

Cenários executados sobre `p1_dev` via HTTP real (Web :5099 → ApiToken → bearer na Api :5100 → Dapper → PG16):

| # | Cenário | Resultado |
|---|---|---|
| E10.0 | Fixture S6 (escola `c1e26-s6-esc` na entidade do contexto, tenant 910001) | exit=0 |
| E10.1 | Cadastro base de produtos via API (`POST /api/educacao/merenda/produtos` ×3) | 200/200/200; ids 33/34/35 no banco |
| E10.2 | **Estoque inicial** (`POST …/estoques` produto A `saldoInicial=100`) | 200; banco `100\|Arroz C1S6` (linha criada espelhando o produto) |
| E10.3 | **Ajuste sobre linha existente** (produto B: `10` e depois `+5`) | 200/200; banco `saldo=15` (soma sobre a mesma linha, sem duplicar) |
| E10.4 | Estoque para produto inexistente (9999999999) | **404 `MERENDA_PRODUTO_INEXISTENTE`** nomeada |
| E10.5 | Cardápio S6 criado e transitado para **ATIVA** (máquina de estados S2) | 200/200 |
| E10.6 | **Distribuição confirmada com consumo real** (2 itens: A−50, B−4) | 200; banco `ATIVA\|2026-11-10\|2 itens\|2 movimentos SAIDA\|saldo total 61` (100−50=50 + 15−4=11) |
| E10.7 | Distribuição com quantidade acima do saldo (A=999) | **409 `ESTOQUE_INSUFICIENTE`** (rollback total; sem baixa parcial) |
| E10.8 | Segunda distribuição no mesmo cardápio × data | **409 `DISTRIBUICAO_DIA_EXISTENTE`** (checagem + backstop de índice) |
| E10.9 | Data fora do período do cardápio | **409 `DATA_FORA_DO_PERIODO`** |
| E10.10 | Item com produto fora do menu do cardápio | **400 `PRODUTO_FORA_DO_MENU`** |
| E10.11 | Distribuição para cardápio ainda `RASCUNHO` | **409 `CARDAPIO_NAO_APROVADO`** |
| CX | Limpeza do fixture (ampliada com movimentos, itens de distribuição, distribuições, estoques, cardápios e produtos S6) | `restantes=0` |

(Blocos E0–E9 das Sprints 1–5 revalidados na mesma rodada: dashboard conjunto, CSV integral,
diagnóstico de schema ausente, auditoria append-only, máquina de estados + guarda de versão, escopo
institucional, unicidade por oferta, transferência com linhagem, correção auditada de frequência,
fechamento/reabertura de diário, publicação de histórico escolar versionado e cadastro base de merenda
— todos PASS.)

## Defeitos descobertos na prova ao vivo e corrigidos (sem máscara)

- **Materialização Dapper × record positional**: a primeira versão lia o cardápio num record
  positional com construtor `(long Id, long? EntidadeId, …)` contra colunas `id, entidade_id, …`.
  O Dapper deste projeto exige para types de construtor que os parâmetros casem **exatamente** com os
  nomes das colunas (sem remoção de underscore): `InvalidOperationException … matching signature
  (System.Int64 id, System.Int64 entidade_id, …)` → 500 genérico em toda chamada de
  `distribuicoes/confirmar`. Corrigido substituindo o record por classe privada com props setáveis e
  **aliases SQL explícitos** (`select id as Id, entidade_id as EntidadeId, …`) — o mesmo padrão
  comprovado na camada avançada da Sprint 4. Regra de projeto aprendida e documentada: leitura
  multi-coluna Dapper usa aliases PascalCase ou tuples posicionais.
- **Driver E2E — variáveis case-insensitive do PowerShell**: o bloco E10 usava `$pA/$pB/$pC` para as
  respostas HTTP e `$pa/$pb/$pc` para os ids lidos no banco; no PowerShell 5.1 são a **mesma**
  variável, e as leituras psql sobrescreviam as respostas HTTP (o check via `code=0`, o exit code do
  psql, com as linhas criadas normalmente). Renomeadas para `$pApiA/$pApiB/$pApiC` com comentário
  explicativo; o driver ganhou também `--max-time 45` e captura de stderr do curl no `Invoke-Api`
  para diagnóstico futuro.
- **Impressão de numeric com escala**: saldos `numeric` imprimiam `100.0000` nos assertions SQL do
  driver; normalizado com `round(…)::text` apenas na camada de evidência (o banco permanece em
  escala inteira por design dos tipos).

## Resíduos declarados (regra 13)

- `validate-script-completop` falha em seed pré-existente `admin@sigov.local` (2 ocorrências já em
  HEAD; não introduzidas nesta sprint) — declarado, não mascarado.
- `rc50_25` permanece arquivo órfã do manifest (intocada, regra 8).
- Requisição de compra derivada da distribuição (ligando consumo→compras) e unidades de consumo por
  aluno/turma ficam para a fatia seguinte do ciclo (S7 cobre complementares A/B — biblioteca).
- RAM local cronicamente baixa (~0,5 GB livre) durante a corrida final — ambiente de dev local, sem
  impacto no artifact; apps reiniciados imediatamente antes da run validada.
