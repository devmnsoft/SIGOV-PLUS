# Contratos da frente administrativa — Compras → Licitação → Contrato → Fornecimento → Recebimento → Estoque/Patrimônio → Obrigação financeira (Ciclo 2, Sprint 7)

**Status:** contrato revisado e catalogado (C2S7, 2026-10-13).
**Natureza:** auditoria funcional escrita — NÃO contém implementação funcional. As Sprints 8–10
implementam os atos tipados abaixo e **devem cumprir este documento**; qualquer desvio exige revisão
deste contrato antes do código (gate da S7: *documento de contratos revisado + permissões da frente
catalogadas em seed + nenhuma implementação fora do catálogo*).
**Base de dados:** migration apenas-de-dados `20261013130000_evo_c2s7_frente_administrativa_permissoes`
(manifest 233) registra o catálogo de permissões da frente em `sigov.permissao` (banco como autoridade —
regra 11) e o vínculo dos perfis SUPERADMIN ativos em `sigov.perfil_permissao` (padrão FUNC03 com ajuste
documentado). Sem alteração de schema.

## 1. Fundamento legal e escopo multi-esfera

- **Lei 14.133/2021** (arts. 6º, 18–29 planejamento, 30–57 procedimento, 100–116 contrato,
  106 prorrogação, 128 processo administrativo único da contratação, 130 sanções);
- **LC 123/2006** (preferência de tratamento a ME/EPP — zona de lances);
- Decreto de pregão eletrônico vigente e jurisprudência TCU/STJ sobre contratação pública.
- **Multi-esfera (regras 21–24):** a frente vale para **municípios** (prefeituras, secretarias,
  autarquias, fundações, câmaras), **estados** (governos estaduais, secretarias estaduais, regionais,
  hospitais, escolas, órgãos públicos e empresas públicas) e **União** (ministérios, autarquias federais,
  fundações, institutos, superintendências, unidades descentralizadas e programas federais). Todo ato da
  frente carrega `tenant_id`, `entidade_id`, `esfera_governo` (`municipal`|`estadual`|`federal`), órgão
  superior quando aplicável, unidade gestora e unidade executora; tetos, percentuais e prazos são
  **parametrizáveis** — nada municipal hardcoded e nenhum valor fixado fora de parâmetro do banco
  (regras 11/12/24).

## 2. Auditoria funcional — estado atual (o que já existe)

A base persistente **já existe** e é mantida sem conversão destrutiva (regra 5):

| Fase | Tabelas existentes (`sigov`, PG16) |
| --- | --- |
| Planejamento (PCA) | `compras_planejamento_contratacao`, `compras_plano_contratacao_anual`, `compras_dfd`, `compras_estudo_tecnico_preliminar`, `compras_mapa_riscos`, `compras_termo_referencia`, `compras_pesquisa_preco`(+`_item`), `compras_modalidade_config`, `compras_parametro_modalidade`, `compras_parametro_criterio`, `compras_recurso` |
| Demanda/solicitação | `compras_demanda`, `compras_solicitacao`(+`_item`,`_historico`), `compras_cotacao`, `compras_fornecedor_cotacao`, `compras_fornecedor` |
| Processo administrativo | `compras_processo`(+`_fase`,`_fase_historico`,`_item`) |
| Procedimento | `compras_licitacao`(+`_item`,`_lote`), `compras_sessao`, `compras_proposta`, `compras_habilitacao`, `compras_julgamento`(+`_rc50_85`), `compras_adjudicacao`, `compras_homologacao`, `licitacao` (legado), `compras_licitapro_*` (apoio: agenda, análise, checklist, documentos, fontes, sincronização) |
| Contrato/ata | `compras_contrato`, `compras_contrato_historico`, `compras_ata_registro_preco`(+`_item`,`_consumo_historico`) |
| Recebimento | `compras_recebimento`(+`_item`) |
| Estoque | `almoxarifado_estoque`, `almoxarifado_movimentacao`, `almoxarifado_material`, `almoxarifado_local`, `almoxarifado_entrega*`, `almoxarifado_requisicao*`, `almoxarifado_transferencia*`, `almoxarifado_auditoria`, `almoxarifado_pendencia_patrimonial` |
| Patrimônio | `patrimonio_bem`, `patrimonio_categoria`, `patrimonio_incorporacao`, `patrimonio_movimentacao`, `patrimonio_movimento`, `patrimonio_baixa`, `patrimonio_inventario*`, `patrimonio_depreciacao`, `patrimonio_imovel*`, `patrimonio_termo_responsabilidade`, `patrimonio_auditoria*` |
| Empenho/orçamento | `empenho`(+`_item`), `financeiro_empenho*`, `orcamento_despesa`, `orcamento_receita`, `orcamento_movimentacao` |
| Dependências | Convênios: `convenio_instrumento`, `convenio_contrapartida`, `convenio_repasse`, `convenio_etapa`, `convenio_prestacao_contas`, `convenio_programa`, `convenio_projeto`, `convenio_meta`, `convenio_auditoria`, demais `convenio_*`; Obras: `obras_contrato_vinculo`, `obras_aditivo`, `obras_reajuste`, `obras_medicao*`, `obras_cronograma*`, `obras_projeto`, `obras_orcamento*`, demais `obra(s)_*`; Jurídico: `juridico_processo`(+movimentos/partes), `juridico_parecer`, `juridico_risco_processual`, demais `juridico_*`; Protocolo: `protocolo*` |

Famílias **fora do escopo desta frente** e não modificadas por ela: `compras_empresarial_*`
(fluxo empresarial B2B) e o módulo legado `estoque_compras` do catálogo SaaS.

**Lacunas que as Sprints 8–10 preenchem sob estes contratos:** comando tipado de demanda com
apuração; sessão de lances com zona ME/EPP documentada; atos separados adjudicação/homologação como
comandos de primeira classe; ciclo de vida completo do contrato administrativo (prorrogação/aditivos/
rescisão/controle de execução); pedido de fornecimento contra item de contrato; recebimento
aprovado/rejeitado por fornecimento com prazo paramétrico; vínculo NF-e/NFS-e com conferência;
destinação em estoque/patrimônio como movimento referenciado único; obrigação financeira única e
idempotente por fornecimento aprovado; contrapartida de convênio como bloqueio de aprovação.

## 3. Contratos por ato (BR-7.1 a BR-7.8)

Cada item define: **atos**, **invariantes**, **chave/erro canônico** (falhas nomeadas explícitas —
regra 13) e **permissão** exigida (catálogo da seção 4). Todas as tabelas de domínio usam PK
`bigint generated ... as identity` (regra 4) e chave multi-esfera da seção 1.

### 3.1 BR-7.1 — Demanda tipada + processo único (S8)
- **Atos:** criar demanda com itens (quantidade > 0, unidade conhecida, preço estimado > 0 **ou**
  pesquisa de preços referenciada, centro de custo válido e ativo, justificativa obrigatória em caso
  de urgência), aprovar (urgência: dupla aprovação — ordenador + autoridade superior), repelir com
  justificativa, reabrir repelida com justificativa; após aprovação, alteração só por **nova versão
  imutável**.
- **Processo único (Lei 14.133 art. 128):** 1 demanda aprovada = 1 processo administrativo (S19) que
  agrega todos os atos da contratação (pesquisa, edital, propostas, decisão, homologação, contrato);
  cronologia única e auditável (`compras_processo` + fases).
- **Invariantes:** quantidade unitária positiva; urgência sem dupla aprovação não aprova; demanda
  cancelável antes da aprovação; após aprovação, imutável.
- **Erros nomeados (exemplo canônico):** `DEMANDA_ITEM_INVALIDO`, `DEMANDA_URGENCIA_SEM_DUPLA_APROVACAO`,
  `DEMANDA_APOS_APROVACAO_IMUTAVEL`, `PROCESSO_JA_VINCULADO_A_DEMANDA`.

### 3.2 BR-7.2 — Procedimento (S8)
- **Atos:** abrir procedimento com modalidade definida por objeto/valor — **pregão eletrônico por
  padrão**, concorrência, consulta prévia até o teto vigente (**teto parametrizado**); habilitação
  (qualificação jurídica, fiscal/trabalhista, técnica) julgada como ato próprio; sessões de lances com
  **zona de preferências ME/EPP (LC 123/2006)** — margem parametrizada (default 5%) e **direito de
  preferência documentado** (exercido ou desistência formalizada fora da margem).
- **Invariantes:** modalidade incompatível com objeto/valor bloqueia abertura; lance só dentro de
  sessão aberta; ordem de preferência registrada por lance (não recalculada a posteriori).
- **Erros nomeados (exemplo):** `MODALIDADE_INCOMPATIVEL_OBJETO_VALOR`, `SESSAO_LANCE_FORA_DE_VIGENCIA`,
  `PREFERENCIA_ME_EPP_NAO_DOCUMENTADA`.

### 3.3 BR-7.3 — Adjudicação e homologação separadas (S8)
- **Atos:** adjudicar (ato do presidente do procedimento) → homologar (autoridade superior distinta —
  separação de funções); contrato só nasce de procedimento **homologado**; vínculo **1 contrato
  vigente × procedimento**.
- **Invariantes:** homologar antes de adjudicar é inválido; procedimento não homologado não gera
  contrato; segunda homologação de mesmo procedimento exige ato de anulação anterior registrado.
- **Erros nomeados (exemplo):** `HOMOLOGACAO_SEM_ADJUDICACAO`, `HOMOLOGADOR_E_ADJUDICADOR_IGUAIS`,
  `CONTRATO_SEM_PROCEDIMENTO_HOMOLOGADO`.

### 3.4 BR-7.4 — Ciclo de vida do contrato (S8)
- **Atos:** criar contrato (partes, valor, prazo, cláusulas administrativas — multa, reajuste,
  rescisão); prorrogação (**Lei 14.133 art. 106 — até 5 anos + 5 prorrogações, ambos parametrizáveis**);
  aditivos — quantitativo (percentual **parametrizado por tipo de objeto**: obras até 50% do valor
  total por força do art. 107 §4, bens/serviços por parâmetro) ou valorativo/reajuste com **índice
  identificado**; rescisão unilateral (inadimplemento, com notificação registrada) ou bilateral;
  controle de execução **financeira × quantitativa (%)** com teto de fornecimentos.
- **Invariantes:** aditivo não excede teto percentual do tipo de objeto; contrato prorrogado além do
  teto paramétrico bloqueia; execução financeira ≠ quantitativa gera desvio visível (auditoria), não
  ajuste silencioso.
- **Erros nomeados (exemplo):** `ADITIVO_EXCEDE_TETO_TIPO_OBJETO`, `PRORROGACAO_EXCEDE_TETO_ART_106`,
  `FONTECIMENTO_EXCEDE_EXECUCAO_DISPONIVEL`, `RESCISAO_SEM_ATO_JUSTIFICATIVA`.

### 3.5 BR-7.5 — Fornecimento e recebimento (S9)
- **Atos:** pedido de fornecimento vinculado a **item de contrato vigente** (quantidade/valor
  disponíveis conferidos na criação); recebimento **parcial** por fornecimento (soma de
  aprovado + rejeitado = recebido; recebido ≤ fornecido); rejeição com **motivo + prazo de
  substituição parametrizado (default 5 dias úteis)** e desfecho obrigatório
  (substituído/acatado); NF-e/NFS-e vinculada ao fornecimento com conferência
  (fornecedor/CNPJ = contratado; valor × pedido).
- **Invariantes:** fornecimento contra item encerrado bloqueia; recebimento sem documento fiscal
  vinculado não liquida; rejeição sem desfecho não fecha o fornecimento.
- **Erros nomeados (exemplo):** `FONTECIMENTO_ITEM_CONTRATO_ENCERRADO`, `RECEBIMENTO_ACIMA_FORNECIDO`,
  `REJEICAO_SEM_DESFECO`, `FATURA_FORNECEDOR_DISCREPANTE`.

### 3.6 BR-7.6 — Destinação em estoque/patrimônio (S9)
- **Atos:** recebimento **aprovado** destina em estoque (mercadoria/serviço-insumo) ou em patrimônio
  (bem permanente/imobilizado com **ficha patrimonial gerada** — código, classe, valor registrado,
  localização, situação); destino total ≤ quantidade aprovada; **cada destino é um movimento
  referenciado único** (1 destino, 1 movimento; sem duplicidade por reprocesso — idempotência por
  referência do destino).
- **Invariantes:** patrimônio sempre com ficha gerada no ato (não manual posterior); destino a
  almoxarifado/local inexistente ou inativo bloqueia; repatriação de pendência patrimonial segue o
  fluxo existente (`almoxarifado_pendencia_patrimonial`).
- **Erros nomeados (exemplo):** `DESTINO_EXCEDE_QTD_APROVADA`, `DESTINO_LOCAL_INATIVO`,
  `FICHA_PATRIMONIAL_OBRIGATORIA`.

### 3.7 BR-7.7 — Obrigação financeira única (S9)
- **Atos:** gerada **uma única vez** por fornecimento aprovado (**idempotente por referência** —
  reprocesso não duplica); valor = liquidado; alimenta **tesouraria (S14)** e **compromisso
  orçamentário (S13 — SP)**; pagamento só com obrigações liquidadas e documentos conferidos.
- **Invariantes:** 1 fornecimento aprovado ≤ 1 obrigação financeira; obrigação de fornecimento sem
  NF-e vinculada não libera pagamento; estorno passa por compensação (não delete físico).
- **Erros nomeados (exemplo):** `OBRIGACAO_DUPLICADA_REFERENCIA`, `PAGAMENTO_SEM_LIQUIDACAO`,
  `PAGAMENTO_DOCUMENTOS_NAO_CONFERIDOS`.

### 3.8 BR-7.8 — Contrapartidas e dependências (S10)
- **Convênios:** quando o instrumento assim definir (**percentual parametrizado por instrumento**),
  a contratação exige **recurso próprio registrado** (`convenio_contrapartida`) — **bloqueio de
  aprovação** sem contrapartida registrada.
- **Obras:** exigem **projeto/licenciamento antes do contrato** (`obras_projeto`); medição/cronograma
  seguem o módulo Obras (vínculo `obras_contrato_vinculo`).
- **Jurídico:** divergências, sanções (art. 130) e rescisões controversas alimentam `juridico_*`
  como dependência, nunca como cópia local.
- **Erros nomeados (exemplo):** `CONTRAPARTIDA_CONVENIO_INEXISTENTE`, `OBRA_CONTRATO_SEM_PROJETO`.

## 4. Catálogo de permissões da frente (seed aplicada — manifest 233)

Autoridade: `sigov.permissao` (regras 11/12). A S7 catalogou **25 chaves novas** no módulo
`compras` (migration `20261013130000`, idempotente, pós-condição `C2S7-POSTCOND` verificando as 25
chaves e o vínculo SUPERADMIN). Vinculação: perfis `SUPERADMIN` **ativos e não excluídos**
(1 por tenant; ajuste documentado — o filtro de origem FUNC03 usava `pa.sistemico`, que é
`false` nas linhas SUPERADMIN do estado atual de homologação; a autoridade é o código externo
`SUPERADMIN`). Perfis operacionais específicos serão configurados pelo banco nas Sprints 8–10
(sem seed funcional).

| Atos (contrato) | Chaves catalogadas (novo) |
| --- | --- |
| Demanda — BR-7.1 (S8) | `compras.demanda.visualizar`, `compras.demanda.criar`, `compras.demanda.aprovar`, `compras.demanda.reabrir` |
| Procedimento — BR-7.2 (S8) | `compras.licitacao.abrir`, `compras.licitacao.modalidade.definir`, `compras.licitacao.habilitacao.julgar`, `compras.licitacao.lances.registrar`, `compras.licitacao.preferencia.documentar` |
| Adjudicação/Homologação — BR-7.3 (S8) | `compras.adjudicacao.executar`, `compras.homologacao.executar` |
| Contrato — BR-7.4 (S8) | `compras.contrato.aditivo.criar`, `compras.contrato.aditivo.aprovar`, `compras.contrato.prorrogar`, `compras.contrato.rescindir`, `compras.contrato.execucao.fiscalizar` |
| Fornecimento/Recebimento/Fatura — BR-7.5 (S9) | `compras.fornecimento.criar`, `compras.fornecimento.visualizar`, `compras.recebimento.aprovar`, `compras.recebimento.rejeitar`, `compras.fatura.vincular` |
| Obrigação financeira — BR-7.7 (S9) | `compras.obrigacao.visualizar`, `compras.obrigacao.liquidar` |
| Contrapartidas — BR-7.8 (S10) | `compras.contrapartida.registrar` |
| Auditoria ponta a ponta (S8+) | `compras.auditoria.visualizar` |

**Chaves já catalogadas e REUTILIZADAS sem alteração** (não entram na seed; o comando correspondente
as consome): `compras.dashboard.visualizar`, `compras.requisicao.{visualizar,criar,aprovar,reprovar}`,
`compras.cotacao.{visualizar,criar}`, `compras.processo.{visualizar,criar,avancar,cancelar}`,
`compras.julgamento.executar`, `compras.contrato.{visualizar,criar}`, `compras.ata.{visualizar,criar}`,
`compras.recebimento.executar`, `compras.exportar`, família legacy UPPER_SNAKE
(`LICITACAO_*`, `CONTRATO_*`, `ATA_REGISTRO_PRECO_*`, `COMPRAS_*`), `almoxarifado.recebimento.confirmar`
(BR-7.6 estoque), `patrimonio.incorporacao.executar` e `patrimonio.bem.criar` (BR-7.6 patrimônio).

## 5. Parâmetros obrigatórios (a implementar em S8–S10, valores por banco — regra 11)

| Parâmetro (código proposto) | Default | Contrato |
| --- | --- | --- |
| `LICITACAO_MARGEM_ME_EPP_PCT` | 5 | BR-7.2 |
| `TETO_CONSULTA_PREVIA_VALOR` | vigente na data | BR-7.2 |
| `ADITIVO_TETO_QUANTITATIVO_PCT_{OBJETO}` | obras 50 (art. 107 §4); bens/serviços parametrizado | BR-7.4 |
| `PRORROGACAO_TETO_ANOS` / `PRORROGACAO_MAX_VEZES` | 5 / 5 (art. 106) | BR-7.4 |
| `RECEBIMENTO_PRAZO_SUBSTITUICAO_DIAS_UTEIS` | 5 | BR-7.5 |
| `CONVENIO_CONTRAPARTIDA_PCT` (por instrumento) | definido no instrumento | BR-7.8 |

Nenhum desses valores pode ser hardcoded em código, view ou tela (regras 11/24).

## 6. O que esta Sprint NÃO faz (gate da S7)

1. Nenhuma implementação funcional (endpoint, tela, domínio, máquina de estados) — apenas o
   catálogo de permissões em seed e este contrato.
2. Nenhuma alteração de schema (PK/íntegridade das tabelas citadas intocadas — migração apenas-de-dados).
3. Nenhum perfil operacional seedado — apenas os perfis SUPERADMIN existentes, por tenant.
4. Nada municipal, estadual ou federal tratado diferentemente fora de parâmetro do banco.

## 7. Evidência e verificação

- Migration aplicada **2×** no PG16 oficial sem erro (idempotência) — pós-condição `C2S7-POSTCOND`
  verde (25 chaves + vínculos = N_perfis_SUPERADMIN × 25).
- Manifest **233 entradas** (232 → 233); consolidados regenerados e verificados
  (`scripts/generate-script-completop.ps1 -Verify`).
- Catálogo no banco: 25 chaves novas ativas em `sigov.permissao` (módulo `compras`); vínculos
  SUPERADMIN completos nos 5 tenants de homologação (125 linhas).
- Relatório da fatia: `docs/evidencias/rcevo/relatorio_ciclo2_fechamento_operacional_sprint7.md`.
