# RELATORIO DO GATE B - RC50.68A (jornada de compras: cotacao -> resposta -> comparativo -> selecao -> pedido -> recebimento)

Data: 2026-10-01 (UTC). Base: `c6407f6c93eb65b094cd043a2a82246e2ffbc614` (main, "melhorias"). Todo o trabalho segue **uncommitted** sobre essa base.

## VEREDICTO

**GATE B = PASS** (run9: **114/114 asserts verdes**, `GATE B: FAILS=0`, exit 0, contra API e banco vivos). Homologacao oficial do conjunto acontece no Bloco D; nada aqui e declarado "pronto" para o SIGOV inteiro.

| Item | Status |
|------|--------|
| Reset limpo (`gate_reset.ps1`) | PASS (`RESET_OK`; ordem de deletes corrigida — bloco pedido antes de fornecedor, FK `compras_pedido_fornecedor_id_fkey`) |
| GATE B run9 (`gate_b.ps1`, S0-S11) | PASS (114/114, `FAILS=0`, zero erros/alertas no log) |
| Build .NET 10 / C# 14 (`sigov.sln`) | PASS (0 erros, execucao fresca desta rodada) |
| Testes oficiais (`dotnet test sigov.sln --no-build`) | PASS (UnitTests 423 + ApiTests 132 + IntegrationTests 141 = **696/696**, 0 falhas; execucao fresca desta rodada) |
| API no ar | `api_pub6` saudavel (health 200); nenhuma mudanca de codigo C# nesta rodada => binario inalterado |
| Catalogo de migrations | static=PASS (SQL=209 manifest=199 baseline=195 governed_orphans=10) |

## O QUE FOI IMPLEMENTADO (Bloco B, diff nao commitado)

### Migration (idempotente, aplicada e sincronizada)
- `database/postgres/migrations/20260930180000_compras_cotacao_itens_selecao.sql`: itens da cotação vinculados aos itens da requisição (`requisicao_item_id`, quantidade reservada), tabela `compras_empresarial_cotacao_selecao` (seleção auditada por fornecedor/item com justificativa e custo), índice/unique necessários à reserva transacional.
- Seed `compras_cotacao_demo` (dados fictícios, idempotente): produtos MUNICIPAIS `e0000001-…-101..104` (+`-201` estadual), fornecedores F1 Alfa RASCUNHO / F2 Beta RASCUNHO / F3 Gama BLOQUEADO, requisições Rq1/Rq2.
- `manifest.json` + os 5 consolidados sincronizados (regra 7).

### Application — `ComprasContracts.cs` + serviços
- Status canônico do ciclo: ABERTA → **EM_RESPOSTA** (≥1 resposta registrada) → SELECIONADA; ABERTA/EM_RESPOSTA → ENCERRADA; convite PENDENTE → RESPONDIDO/REVOCADO (EXPIRADO virtual pelo prazo).
- Cotação só de requisição APROVADA com saldo transacional por item (reserva por item: ABERTA/EM_RESPOSTA reserva o total; SELECIONADA mantém somente os itens selecionados; ENCERRADA libera tudo; pós-seleção integral → 422 "integralmente reservados"; rodada nova = max(rodada)+1).
- Fornecedor por nome/documento; proposta registrada internamente por operador autorizado (não chega como HTTP externo).
- Fórmula explícita (texto canônico em `ComprasContracts.cs` L230–233): `CUE = PU × (1 + imp/100 − desc/100)` (2 casas, AwayFromZero); `custo_total_item = CUE × qtd + frete`; comparação pelo menor `custo_total_item` entre ofertas não recusadas; **empate sinalizado** quando ≥2 empatam.
- Seleção humana e auditada, transacional única: justificativa (trim ≥10) obrigatória quando custo > menor; version mismatch → 409; item duplicado → 400; nova seleção pós-SELECIONADA → 422.
- Idempotência nas operações `COTACAO_CRIAR/RESPONDER/SELECIONAR/ENCERRAR` e `FORNECEDOR_CRIAR`: replay mesma chave + mesmo hash (SHA256 do JSON camelCase) → 201 `repetido=true` reexpondo o original; mesma chave + hash diferente → 409. Ordem lock → idempotência → guards.
- Pedido atômico e idempotente (um por fornecedor selecionado); reconhecido pelo módulo EXISTENTE de recebimento legado (`compras_recebimento`/`compras_empresarial` ad-hoc vazias na jornada); sem estoque nem obrigação financeira automáticas.

### Infrastructure — `CotacaoCompraRepository.cs` (nova)
- Repositório Dapper dos fluxos de cotação/resposta/seleção/pedido/recebimento; mapeamento PascalCase nos aliases (regra canônica Dapper 2.1.35 — sem normalização de underline).
- **BUG D corrigido (L291)**: upsert de `cotacao_resposta_item` na revisão de proposta usava `version = sigov.compras_empresarial_cotacao_resposta_item.version + 1` — version stale fazia 409 permanente na revisão; agora incrementa corretamente no próprio upsert.

### Api / Web
- 13 rotas em `ComprasEmpresariaisController` (Api) com policies de autorização do banco, antiforgery e mapa de erros canônico (Concurrency→409, KeyNotFound→404, UnauthorizedAccess→401, Argument→400, InvalidOperation→422, resto 500; shape `{status,title,detail}`).
- 6 views Web + `compras-cotacoes.js` (telas reais: lista/nova/detalhe/comparativo/pedidos) — polimento de UX completo fica no Bloco C.

### BUGS A–E (encontrados e confirmados corrigidos pelo gate)
A–E documentados na rodada de correção pré-run8 (incl. BUG D acima); todos cobertos por asserção literal no `gate_b.ps1` e verdes no run9.

## CENARIOS DO GATE B (todos PASS no run9)

- **S0** Preflight estado virgem pós-reset (fixtures RASCUNHO v1, sem política/cotação/pedido/recebimento, chaves livres).
- **S1** Política institucional: upsert por tenant, alçadas cumulativas 50000/250000, replay idempotente.
- **S2** Aprovação: Rq1 (80000 → 2 níveis) e Rq2 (40000 → 1 nível); Rq3/Rq4 permanecem RASCUNHO.
- **S3** Elegibilidade + criação C1 sobre Rq1 (2 itens): negativas (prazo >90d, sem Idempotency-Key, requisição não aprovada/saldo), 201 com `numero` gerado, replay, conflito de chave, segunda cotação ativa na mesma requisição → 422 com mensagem exata.
- **S4** Respostas da C1: stale version, propostas internas Alfa/Beta, replay, conflito, revisão antes do julgamento.
- **S5** Comparativo da C1: fórmula impressa, menor custo por item, **EMPATE sinalizado**, valores conferidos independentemente em PS.
- **S6** Seleção auditada da C1: negativas (item duplicado, justificativa curta, stale version, sem permissão), seleção real → 2 pedidos (1 por fornecedor).
- **S7** C2 sobre Rq2: oferta RECUSADA excluída do comparativo; encerramento libera saldo; C3 (rodada=2) conclui.
- **S8** Pedidos: lista (3 CONFIRMADO, todos vinculados a cotação+requisição), detalhes (valor total, previsão, itens atômicos: qtd/valor unitário/exclusão de cancelada), 404, isolamento por contexto, auditoria — **incl. sub-condição b4 (`numeroCotacao`) que falhou no run8**.
- **S9** Recebimento EXISTENTE reconhece o pedido (sem estoque e sem obrigação financeira automáticas).
- **S10** Fornecedor via API (`FORNECEDOR_CRIAR`, 4º fornecedor Delta FOR-2026-000003): nasce RASCUNHO, replay, conflito de chave, campos obrigatórios.
- **S11** Snapshot final do banco + resumo (abaixo).

### Snapshot final (run9)
- Cotações: `CT-2026-000013` r1 SELECIONADA (Rq1), `CT-2026-000014` r1 ENCERRADA (Rq2), `CT-2026-000015` r2 SELECIONADA (Rq2).
- Seleções (3): IT1→Beta qtd10 custo 900.00; IT2→Alfa qtd5 custo 250.00; IT3(Rq2)→Alfa qtd8 custo 1075.20.
- Ledger de idempotência: 20 chaves (APROVACAO/COTACAO/FORNECEDOR/REQUISICAO), todas com `resultado` persistido.
- Histórico: COTACAO CRIADA 3 / ENCERRADA 1 / RESPOSTA_REGISTRADA 7 / SELECAO_REGISTRADA 2; FORNECEDOR CRIADO 4 (3 seed + Delta); PEDIDO CRIADO 3.
- Última linha do log: `GATE BLOCO B RESUMO: PASS - todas as assercoes verdes (jornada requisicao -> cotacao -> comparativo -> selecao -> pedido -> recebimento)`.

## ROOT CAUSE DA ULTIMA FALHA (run8, assert pedido Beta b4)

O único FAIL do run8 (`numeroCotacao=[]`) **não era da API**: variáveis do PowerShell 5.1 são case-insensitive — em L234 do gate, `$C1=[string]$c1.json.id; $NUMC1=[string]$c1.json.numero`, a primeira statement escreve em `$C1` (≡ `$c1`), sobrescrevendo o objeto JSON com a string do id; a segunda lê `.numero` de uma string, que em modo não-strict retorna `$null` silenciosamente. Parse (AST) perfeito, bug 100% semântico. Fix: snapshot antes (`$c1j=$c1.json`). Confirmado com probes lado a lado e bisect; instrumentação DBG-B1 removida; detectors `chk_collision.py`/`chk_casefile.py` reportam 0 colisões ativas nos gates. Consolidado em `Temp/opencode/gate_pitfalls.md` (também: quirk `.Count` com 1 objeto, logs UTF-16LE, shell externo PS 5.1 sem `&&`, `.ps1` sem BOM lido como ANSI).

## EVIDENCIAS

- `Temp/opencode/gate_b_run9.log` (UTF-16LE; dumperes `dump_run9.py`/`dump_run9b.py`): 340 linhas, 114 `ASSERT [PASS]`, 0 `ASSERT [FAIL]`, sem `!!`/erros/exceções.
- `Temp/opencode/gate_reset_run9.log`: `RESET_OK`.
- `Temp/opencode/jornada_evidence.txt`: append do run9 (SQL/GET + ASSERTs + RESUMO PASS no final do arquivo).
- Catalogo: `scripts/check-migration-catalog.sh` static=PASS (SQL=209 manifest=199 baseline=195 governed_orphans=10).
- Imagem API: `api_pub6` (hashes: manifest `f2e8a86e…`, Sigov.Infrastructure.dll `dbca5f50…`); sem mudança de código nesta rodada.

## LIMITACOES DOCUMENTADAS (nao sao defeitos; nao bloqueiam o GATE B)

- **GAP DE TABELAS BASE para o Bloco D**: nenhuma migration/consolidado contém os `CREATE TABLE` das tabelas BASE `compras_empresarial_*` (só existiam no banco live; constraint legacy `ce_compras_cotacao_resposta_item_pkey` indica rename fora do catálogo; insert de pedido escreve `valor_total` E `total`). Corrigir com migration corretiva idempotente (pg_dump schema live → `create table if not exists` + índices + ledger) + sync dos 6 consolidados + manifest ANTES da validação de instalação limpa.
- `db-migrations` sai Exited(3) (lacuna preexistente de ledger anterior a esta RC): API iniciada com `docker start sigov-api`; comportamento documentado e não mascarado.
- FKs inconsistentes no banco live (ex.: `compras_empresarial_pedido` referencia apenas `fornecedor`); `compras_empresarial_recebimento*` permanece vazia na jornada — "pedido reconhecido pelo recebimento" usa o módulo legado (`compras_recebimento`/`compras_recebimento_item`).
- Família legado `compras_*` mapeada no banco live: 53 tabelas (incl. `compras_licitacao*`, `compras_proposta`, `compras_recebimento*`, `compras_contrato*`, `compras_pesquisa_preco*`).

## FORA DO RECORTE DO GATE B (vai para Blocos C/D)

- **Bloco C**: contexto/UX — cabeçalho organização/unidade/exercício/modo, menu por operações autorizadas (corrigir marcações ativas múltiplas), ajuda em toda tela alterada, seletores nominais, dados preservados após erro, claro/escuro, teclado/foco/leitor de tela, breakpoints 1440/768/390px.
- **Bloco D**: migration corretiva das tabelas base + validações limpa/upgrade/reexecução, constraints/índices, isolamento, compatibilidade com registros antigos, `check-migration-catalog.sh` + `check-api-route-conflicts.sh`, build + testes oficiais frescos e relatório final Implementado/Testado/Homologado/Bloqueado/Fora do recorte. Depois priorizar: pedido → recebimento → divergência/devolução → fechamento.
