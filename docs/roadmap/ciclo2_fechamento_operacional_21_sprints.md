# Ciclo 2 — Fechamento Operacional do SIGOV PLUS — Roadmap 21 Sprints

> Objetivo: transformar o SIGOV PLUS no maior SaaS de administração pública do Brasil —
> multi-esfera (município/estado/União, regra 21), multi-jurisdicional (arquitetura pronta
> para exportação: catálogo jurídico por país, LGPD/GDPR, moeda, idioma, calendário),
> com integridade transacional verificável em toda regra de negócio.

## Premissas globais (todas as sprints)

1. .NET 10 (`global.json`) + C# 14 + Dapper + PostgreSQL 16 (sem EF Core). PK `bigint generated ... as identity`.
2. Migration idempotente obrigatória quando houver mudança de schema; `manifest.json` + 6 consolidados
   sincronizados (regra 7); migration publicada nunca alterada (regra 8).
3. Falhas nomeadas explícitas: todo estado impossível gera código nomeado
   (`CODIGO_NOMEDADO: mensagem acionável`) — regra 13. Zero simulação de sucesso.
4. Banco como autoridade de autorização/perfis/permissões (regra 11); seed de permissão por módulo, idempotente.
5. Nada municipal hardcoded (regras 21–24): toda regra parametrizada por esfera/jurisdiação.
6. Idempotência padrão: reenvio do mesmo comando retorna o efeito original (mesmo id), sem duplicar;
   chave de idempotência (campo ou corpo) + backstop de índice parcial único no banco.
7. Tempo único por sprint (fonte operacional configurável); relógio da camada que exerce a regra.
8. Sem novas classes de teste — estender classes existentes (regra 14); suítes mantêm baseline verde.
9. Gates por sprint (RC própria, regra 20): build Release `-warnaserror` 0w/0e; suítes verdes;
   E2E Tier-2 com baseline preservado + blocos novos; migration aplicada 2× em `p1_dev`;
   relatório `docs/evidencias/rcevo/relatorio_ciclo2_fechamento_operacional_sprintN.md`; commit
   `RC-EVO Ciclo 2 Sprint N: ...` (msg UTF-8 sem BOM).
10. Níveis de evidência por funcionalidade (nunca promover módulo inteiro por endpoint isolado):
    **IMPLEMENTADO → VALIDADO POR TESTES → EXECUTADO EM INTEGRAÇÃO → HOMOLOGADO PELA INTERFACE → BLOQUEADO**.
11. Fora de escopo deste ciclo: credencial de produção (adiada, S21 prepara o plano);
    correções CSV/totais/matrícula do Ciclo 0 não se repetem como novas.

---

## SPRINT 1 — Integridade transacional da merenda (estoque × movimento × estorno × idempotência)

**Legislação:** LC 11/1996 c/ Lei 11.947/2009 (PNAE); LC 114/2021 (FUNDEB); normas FNDE/Sismar;
rastreabilidade cardápio → previsão → consumo → perdas; mínimo de 30% dos recursos federais do
PNAE para agricultura familiar/produtor local (LC 11/96 art. 14, parâmetro por esfera).

**Regras de negócio:**
- BR-1.1 Comandos tipados de estoque: `ABERTURA` (só se não houver linha vigente para
  tenant × produto; cria linha + movimento `ENTRADA`), `ENTRADA` (exige linha aberta;
  saldo += qtd; movimento `ENTRADA`), `AJUSTE` (exige justificativa; qtd ≠ 0, pode ser negativa;
  saldo += qtd assinada; movimento `AJUSTE`).
- BR-1.2 Movimento e saldo são gravados **na mesma transação**; falha em qualquer um desfaz tudo.
- BR-1.3 Linha de saldo é única por tenant × produto (backstop índice parcial único;
  deduplicação legada idempotente somando saldos na menor id). Criação concorrente: lock
  `for update` + retry único convertendo colisão 23505 em acréscimo — nunca duplica linha.
- BR-1.4 Toda baixa atualiza exatamente a linha travada (`n==1` verificado); item cuja gravação
  afete 0 linhas aborta a transação (produto inativado concorrentemente).
- BR-1.5 Distribuição revalida **dentro da transação**: cardápio `for update` (status ativo,
  período, escopo institucional), dia livre, itens do menu, estoque suficiente — nesse ordem.
- BR-1.6 Idempotência de distribuição: `chaveIdempotencia` opcional persistida na distribuição.
  Reenvio com mesma chave + mesmo conteúdo (cardápio/data/itens) → retorna o id original,
  sem nova baixa; mesma chave + conteúdo diferente → `CHAVE_IDEMPOTENCIA_CONFLITO` (409).
  Dia ocupado sem chave correspondente → `DISTRIBUICAO_DIA_EXISTENTE` (409), como hoje.
- BR-1.7 Estorno de movimento (estoque): comando tipado `POST /movimentos/{id}/estornar` com
  justificativa obrigatória; cria exatamente 1 movimento `ESTORNO` (qtd = −base) por base
  (backstop índice parcial único `(tenant, referencia_id, produto_id) where tipo='ESTORNO'`);
  estorno repetido → retorna o id do estorno existente (compensação única); estorno de estorno
  ou de tipo indevido → falha nomeada; saldo negativo pós-estorno → `SALDO_NEGATIVO_REJEITADO`.
- BR-1.8 Estorno de distribuição: comando tipado `POST /distribuicoes/{id}/estornar` —
  restaura o saldo de cada item (lock + `n==1`), grava movimento `ESTORNO` por item com
  justificativa, marca a distribuição `CANCELADA` com justificativa; documento e itens
  permanecem íntegros (auditoria append-only); reestorno → efeito único; distribuição já
  cancelada sem efeitos → `DISTRIBUICAO_ESTORNO_INDISPONIVEL`.
- BR-1.9 Cancelamento genérico de distribuição COM efeitos via endpoint genérico →
  `DISTRIBUICAO_ESTORNO_OBRIGATORIO` (409): compensação só pelo comando tipado.
  Sem efeitos (sem movimentos) segue permitido.
- BR-1.10 Endpoint legado `POST /estoques` mantém semântica de soma (baseline E2E), agora
  transacional, com movimento gravado e conexões sempre descartadas.

**Falhas nomeadas:** `ESTOQUE_ABERTURA_EXISTENTE`, `ESTOQUE_NAO_ABERTO`, `SALDO_NEGATIVO_REJEITADO`,
`CHAVE_IDEMPOTENCIA_CONFLITO`, `ESTORNO_BASE_INEXISTENTE`, `ESTORNO_BASE_INVALIDA`,
`DISTRIBUICAO_ESTORNO_OBRIGATORIO`, `DISTRIBUICAO_ESTORNO_INDISPONIVEL` + mantidas do S6.

**Cenários de prova (E2E):** reenvio → 1 baixa; estorno repetido → 1 compensação; ajuste concilia
movimento×saldo (soma dos movimentos ≡ saldo); duas aberturas paralelas → 1 linha e saldo somado;
cancelamento genérico com efeito bloqueado.

**Schema:** migration (a) coluna `chave_idempotencia` em `educacao_merenda_movimento` + índice
único parcial por tenant; (b) índice parcial único de estorno por base; (c) dedupe + índice único
parcial `tenant × produto` no estoque. POSTCOND explícito.

---

## SPRINT 2 — Máquina de estados por recurso, fim dos bypasses genéricos, tempo único

**Referência:** doutrina de integridade (invariantes executadas na fonte de autoridade);
padrão CQRS-lite (comandos tipados como única porta de mutação com efeito).

**Regras de negócio:**
- BR-2.1 Toda família de recursos tem matriz de transição **com efeitos declarados**
  (o que muda fora do registro ao entrar no estado alvo). Ex.: distribuição ATIVA→CANCELADA
  exige estorno; empréstimo ATIVO→DEVOLVIDO devolve exemplar; receita FUNDEB→CANCELADA libera
  unicidade de competência/fonte. `_ => true` para origem desconhecida é eliminado: origem
  desconhecida vira estado `LEGADO_NAO_REGULARIZADO` (visível, tratável, não silenciosa).
- BR-2.2 Endpoint genérico de criação `POST /operacoes/{recurso}` restrito: recursos com
  invariante tipada (movimento, estoque, distribuição e itens, empréstimo, reserva, ocorrência,
  viagem, custo, valor de indicador…) passam a exigir o comando tipado — criação genérica nesses
  recursos responde `COMANDO_TIPADO_OBRIGATORIO` (409) com a rota correta na mensagem.
  Recursos realmente livres (fornecedor, centro de custo…) seguem genéricos.
- BR-2.3 Endpoint genérico de status: mantém papéis de transição simples, mas aplica a guarda
  de efeitos da BR-2.1 antes de gravar; `updatedAt` (controle de concorrência otimista) passa a
  ser exigido quando o registro já foi atualizado antes (`updated_at is not null`) —
  divergência → `CONFLITO_VERSAO` (409) em vez de `is null or`.
- BR-2.4 Estados terminais são irreversíveis, salvo reabertura explícita permitida pela família
  (cardápio ENCERRADA→ATIVA existe por projeto S5 e permanece; demais famílias sem exceção).
- BR-2.5 Tempo único: fonte operacional de data/hora centralizada e configurável
  (`Clock` com escopo DI; SQL usa `now()`/`current_date` só onde o banco exerce a regra).
  Biblioteca (`DateTime.Today` C#) e elegibilidades SQL (UTC) passam a compartilhar a fonte;
  o driver E2E reflete a fonte da camada que exerce.
- BR-2.6 Schema ausente: diagnóstico uniforme para todos os recursos
  (`RECURSO_INDISPONIVEL` 404 com a mesma mensagem/código padrão; hoje varia por caminho).
  Zero diferença observável entre "tabela ausente" nos endpoints listar/criar/status/dashboard.

**Cenários de prova:** cada bypass do briefing (criar movimento/empréstimo/distribuição via
genérico) responde falha nomeada; transição desconhecida → falha (não `_ => true`);
concorrência de update com versão divergente → 409; troca de fonte de tempo não muda resultado
de elegibilidade dentro do mesmo dia operacional.

**Gates extras:** matriz documentada em `MATRIZ_FLUXOS_VERTICAIS.md` por família × estado ×
efeito; nenhum teste novo por classe — ampliar classes existentes.

---

## SPRINT 3 — Biblioteca completa (circulação real, inventário, ocorrências, histórico)

**Referência:** FINEB/FNLI (política nacional do livro didático — 5% do FUNDEB para biblioteca),
diretrizes ABIA, boas práticas de circulação escolar/universitária.

**Regras de negócio:**
- BR-3.1 Empréstimo: tomador exatamente um de aluno/servidor do tenant; prazo ≥ data de
  lançamento; máximo de empréstimos vigentes por tomador (parâmetro do tenant, default 2);
  unicidade de vigência por exemplar (já existente) e por fila de reserva tamanho 1 (já existente).
- BR-3.2 Devolução: idempotente (repetida retorna o evento original; sem duplicar ocorrência),
  calcula atraso real (dias × data de devolução efetiva, não de registro), libera exemplar e
  transfere automaticamente a reserva vigente para empréstimo ao tomador reservante (novo
  empréstimo vinculado à reserva; reserva é consumida).
- BR-3.3 Renovação: só com exemplar vigente, limite de N renovações por empréstimo
  (parâmetro, default 1), sem reserva pendente no exemplar; renovação estende o prazo a partir
  da data original + N dias (não a partir da data atual — evita renovação infinita);
  histórico append-only.
- BR-3.4 Ocorrências: PERDA, DANO, EXTRAVIO, BLOQUEIO — com motivo, valor estimado e data;
  perda/dano encerra o empréstimo vigente automaticamente (compensação única) e move o exemplar
  para status correspondente; bloqueio impede empréstimo/reserva (falha `EXEMPLAR_BLOQUEADO`);
  desbloqueio exige justificativa.
- BR-3.5 Inventário físico: snapshot datado do estado do sistema (exemplar × status) gerado em
  um comando; contagem física lançada contra o snapshot (1 comando idempotente por exemplar ×
  inventário); divergências (falta/sobra/status divergente) listam as duas posições; ajuste
  individual por divergência com justificativa — inventário só fecha quando todas as
  divergências têm desfecho (ajuste ou dispensa justificada).
- BR-3.6 Disponibilidade consistente: dashboard "disponível" = exemplares fiscais ATIVOS
  − vigentes em empréstimo − reserva pendente − bloqueados (mesma expressão SQL em todas as
  telas; zero ≠ indisponível — vazio mostra "sem exemplares", schema ausente mostra código).
- BR-3.7 Histórico: timeline única por exemplar (empréstimos, devoluções, renovações,
  ocorrências, inventários) e por tomador; somente leitura, append-only.
- BR-3.8 Acervo FINEB opcional: marcador `origem=FNLI/5%FUNDEB` no acervo para prestação de
  contas (integração futura com o módulo de compras — não bloqueia circulação).

**Cenários de prova:** 2 empréstimos → 1 vigente; 2 devoluções → 1 evento; renovação além do
limite bloqueada; reserva consumida uma única vez na devolução; inventário com falta → fluxo
completo até fechamento; estorno de ocorrência de perda devolve exemplar (comando próprio).

---

## SPRINT 4 — Transporte escolar: da rota à viagem (recursos reais, presença, custos)

**Referência:** recursos do transporte escolar (MEC/secretarias estaduais e municipais),
CONTRAN/CBH para veículos, segurança viária escolar (parâmetros por esfera/jurisdicação).

**Regras de negócio:**
- BR-4.1 Veículo: placa, capacidade, categoria; situação documental (vencida/baixa) impede
  alocação (`VEICULO_INAPTO`); inspeção/revisão com validade (parametrizável) — vencida alerta
  e bloqueia (configurável alerta/bloqueio).
- BR-4.2 Motorista: habilitação (categoria) e validade da licença — vencida impede viagem
  (`MOTORISTA_HABILITACAO_VENCIDA`); vínculo com veículo permitido apenas para categorias
  compatíveis com o veículo (tabela categoria→classe de veículo configurável).
- BR-4.3 Viagem: programa rota + data + veículo + motorista + monitor(es); **substituição sem
  sobreposição**: não pode existir outra viagem válida do mesmo veículo ou motorista na mesma
  data (backstop índices parciais por veículo×data e motorista×data); criação valida capacidade
  (vinculados ativos da rota ≤ capacidade) e aptidão dos recursos.
- BR-4.4 Presença por viagem: lançamento individual por aluno vinculado (presente/faltante/
  desligado no dia); presença de transporte é domínio próprio — não altera frequência escolar
  (S15), apenas correlaciona; fechamento de presença impede novo lançamento (estorno com
  justificativa desbloqueia um registro por vez).
- BR-4.5 Ocorrências: TIPO (ACIDENTE, QUEBRA, CLIMA, ROTINEIRA_OUTRA), GRAVIDADE, descrição,
  data/hora, viagem vinculada; ocorrência grave (ALTA) muda a situação da viagem
  (`OCORRENCIA_GRAVE`) e exige desfecho (relatório) antes de permitir novas viagens do mesmo
  veículo antes de 24h (parametrizável).
- BR-4.6 Custos: estimativa (km × tarifa + combustível estimado + pedágio previsto) gravada no
  planejamento; realizado é entrada de custo efetivo (nota/recebimento referenciado quando
  disponível); **custo estimado ≠ realizado** — dois conjuntos distintos comparáveis em
  relatório (desvio absoluto e %); custo realizado só via comando próprio (sem edição manual
  do valor).
- BR-4.7 Ciclo da viagem: PROGRAMADA → EM_ANDAMENTO (início, com hora) → CONCLUIDA (fim, com
  hora e km percorridos); cancelamento (justificativa) antes de início libera veículo/motorista;
  após início, cancelamento exige ocorrência vinculada; substituição de veículo/motorista gera
  nova versão da viagem mantendo a cronologia (append-only) e a presença migrada.
- BR-4.8 Relatório de viagem: alunos transportados (presença), ocorrências, custos estimado ×
  realizado, km; exportável; origem verificável (query + timestamp).

**Cenários de prova:** sobreposição de veículo rejeitada; sobreposição de motorista rejeitada;
viagem sem monitor (quando exigido pela esfera) rejeitada; presença duplicada idempotente;
custo realizado não altera estimado.

---

## SPRINT 5 — FUNDEB/custos/rateio + avaliação do magistério + maturidade de indicadores/Educacenso

**Legislação:** LC 114/2021 (FUNDEB — 70% dos recursos aplicados em remuneração básica de
profissionais da educação em carreira; complementação da União/VADT), Lei 11.494/2007 (regul.
FUNDEB), INEP (Censo Escolar, IDEB), MROSD.

**Regras de negócio:**
- BR-5.1 Parâmetros FUNDEB por esfera (valor per capita anual/mensal, mínimos de aplicação,
  alíquotas de complementação) com vigência por ano e unicidade vigente por código (já existente).
- BR-5.2 Receitas/aplicações FUNDEB: unicidade competência × fonte (receita) e competência ×
  centro (aplicação) — comandos tipados já existentes; cancelamentos liberam a unicidade;
  competência fechada (período encerrado no calendário) bloqueia novos lançamentos
  (`COMPETENCIA_FUNDEB_FECHADA`).
- BR-5.3 Indicador de conformidade: % de aplicação em remuneração básica de profissionais da
  educação (aplicações de centros classificados como MAGISTERIO ÷ total de aplicações)
  comparado ao mínimo (default 70%, paramétrico por ano/esfera); painel mostra fórmula,
  numeração, denominação e posição (ALERTA abaixo do mínimo); recorte por unidade escolar e
  por competência; zero de aplicações ≠ indisponível (mostra 0% com nota "sem aplicações").
- BR-5.4 Rateio de custos: centros de custo existentes; critério (MATRICULAS, RECEITA,
  PESO_MANUAL) com pesos por centro; rateio calcula valores proporcionais cujo **soma é igual
  ao total lançado** (arredondamento absorvido no maior peso — sem centavo perdido);
  idempotência: 1 rateio por centro de origem × critério × competência (`RATEIO_DUPLICADO`);
  competência fechada bloqueia; estorno de rateio devolve os lançamentos derivados (comandado).
- BR-5.5 Avaliação do magistério: avaliação (nome, período, unidade/escopo, instrumento),
  resultados por servidor (nota, competência avaliada), periodicidade por ano (1 resultado por
  servidor × avaliação); fechamento da avaliação trava resultados e publica consolidado
  por unidade (média, participação %) — imutável após fechamento; reabertura só para
  retificação com justificativa registrada.
- BR-5.6 Educacenso maturidade: lote por ano (existente); inconsistências com GRAVIDADE e
  ORIGEM (existente); lote só encerra quando inconsistências ALTA zeradas (BAIXA/MÉDIA exigem
  decisão documentada: correção ou aceite com motivo); eventos de sincronização registrados;
  painel mostra posição (lote ativo, altas abertas, última sincronização).
- BR-5.7 Projeção de matrícula (existente) ganha uso real: alimenta o indicador de
  custo por aluno projetado (S20) e o plano de oferta — uma projeção vigente por escola × ano.

**Cenários de prova:** conformidade abaixo do mínimo gera ALERTA com números corretos;
rateio soma bate com o total (centavo por centavo); rateio duplicado bloqueado; lote Educacenso
com alta aberta não encerra; avaliação fechada não aceita resultado novo.

---

## SPRINT 6 — Jornadas pelas telas reais (substituir a fachada estática)

**Regra 8 do complemento (relatórios com origem verificável) + design system compartilhado.**

**Regras de negócio:**
- BR-6.1 As 7 áreas (Merenda, Biblioteca, Transporte, FUNDEB/Custos, Indicadores/Educacenso,
  Matrícula, Processos-Visão Geral quando existirem) deixam de renderizar o shell estático
  `Views/Educacao/Avancada.cshtml`; cada controller renderiza sua view ligada aos endpoints
  tipados; KPI com origem (nome da métrica + query/resumo + timestamp) — **zero ≠ indisponível**.
- BR-6.2 Toda ação da tela chama API real e exibe a resposta real (sucesso com id/efeto; erro
  nomeado mapeado para mensagem acionável); zero toast falso ("Painel atualizado" sem chamada).
- BR-6.3 Responsivo 360/768/1440 + navegação completa por teclado (foco visível, atalhos,
  modal acessível); componentes compartilhados (`_FilterBar`, `_ConfirmModal`, `_EmptyState`,
  `_Loading`, `_ModuleHelpBox`, cartões de KPI) — sem CSS inline por página.
- BR-6.4 Confirmação obrigatória para ações destrutivas (estorno, cancelamento, encerramento,
  exclusão lógica) com justificativa quando a regra exigir; loading durante chamada; erro
  mantém o estado do formulário.
- BR-6.5 Cada jornada concluída pela interface (menu → lista → detalhe → ação → confirmação →
  estado atualizado) e capturada em screenshot por perfil; a jornada HTTP só vale como
  INTEGRADO, a via interface vale como HOMOLOGADO PELA INTERFACE.
- BR-6.6 Menu/grupo "Educação Avançada" (`_Sidebar.cshtml`) mantém estrutura; rotas por área;
  acesso governado por permissões do banco (regra 11) — perfil sem permissão vê estado
  "sem acesso" nomeado, não erro 500.

**Cenários de prova:** 7 jornadas E2E via interface (screenshot + HTTP), 3 resoluções ×
teclado, permissão ausente → 403/estado amigável.

---

## SPRINT 7 — Mapa da frente administrativa (contratos de auditoria, sem implementação)

**Entrega:** auditoria funcional escrita de Compras (Lei 14.133/2021) → Licitação → Contrato →
Fornecimento → Recebimento → Estoque/Patrimônio → Obrigação financeira, com dependências
(Jurídico, Convênios, Obras).

**Regras (contratos que as sprints 8–10 devem cumprir):**
- BR-7.1 Demanda tipada (item: quantidade, unidade, preço estimado, justificativa, centro de
  custo, urgência) com aprovação; 1 demanda = 1 processo administrativo (S19) — processo único
  da contratação (Lei 14.133 art. 128).
- BR-7.2 Procedimento: modalidade por objeto/valor (pregão eletrônico default; concorrência;
  consulta prévia até o teto vigente); habilitação (qualificação jurídica, fiscal/trabalhista,
  técnica); lances com **zona de preferências ME/EPP** (LC 123/2006 — margem default 5%,
  parametrizável) e direito de preferência documentado.
- BR-7.3 Adjudicação + homologação são atos separados e ordenados (adjudicar → homologar);
  contrato só nasce de procedimento homologado; vínculo 1 contrato vigente × procedimento.
- BR-7.4 Contrato: partes, valor, prazo, cláusulas administrativas (multa, reajuste,
  rescisão), prorrogação (Lei 14.133 art. 106 — até 5 anos + 5 prorrogações, paramétrico);
  aditivos (quantitativo/até 25% em serviços? — percentuais paramétricos por tipo de
  objeto; valorativo/reajuste com índice identificado); controle de execução (financeira ×
  quantitativa %).
- BR-7.5 Fornecimento: pedido de fornecimento vinculado a item de contrato (quantidade/valor
  disponíveis); recebimento parcial por fornecimento; rejeição com motivo + prazo de
  substituição (paramétrico, default 5 dias úteis) e desfecho (substituído/acatado); NF-e/NFS-e
  vinculada (fornecedor/CNPJ/conferência de valor vs pedido).
- BR-7.6 Destinação: recebimento aprovado destina em estoque (mercadoria/serviço-insumo) ou
  patrimônio (bem permanente/imobilizado com ficha patrimonial gerada: código, classe, valor
  registrado, localização, situação) — destino total ≤ quantidade aprovada; cada destino é um
  movimento referenciado (1 destino, 1 movimento; sem duplicidade por reprocesso).
- BR-7.7 Obrigação financeira: gerada **uma única vez** por fornecimento aprovado (idempotente
  por referência), valor = liquidado; alimenta tesouraria (S14) e compromisso orçamentário (S13
  — SP); pagamento só com obrigações liquidadas e documentos conferidos.
- BR-7.8 Contrapartidas: convênio (S10) exige recurso próprio quando o instrumento assim
  definir (percentual paramétrico) — bloqueio de aprovação sem contrapartida registrada; obra
  (S10) exige projeto/licenciamento antes do contrato.

**Gates:** documento de contratos revisado + permissões da frente catalogadas (seed) + nenhuma
implementação fora do catálogo.

---

## SPRINT 8 — Compras integradas (1/2): demanda → contratação → contrato

**Legislação:** Lei 14.133/2021 (arts. 6º, 18–29, 30–57, 100–116, 128, 130), LC 123/2006,
Decreto de pregão eletrônico vigente, jurisprudência TCU/STJ sobre contratação.

**Regras de negócio (além dos contratos da S7):**
- BR-8.1 Demanda: criação tipada com itens (validação: quantidade > 0, unidade conhecida,
  preço estimado > 0 ou pesquisa de preços referenciada, centro de custo válido e ativo,
  justificativa para emergências); estados SOLICITADA → APROVADA/REPELIDA (justificativa) →
  CONTRATADA; reabertura com justificativa; 1 demanda → N itens; cancelamento antes da
  aprovação; após aprovação, alteração só por nova versão (imutável).
- BR-8.2 Processo único: demanda aprovada abre/vincula processo administrativo (S19) que
  agrega todos os atos (pesquisa, edital, propostas, decisão, homologação, contrato) —
  cronologia única e auditável.
- BR-8.3 Procedimento: criação tipada (objeto, modalidade, fonte de recurso, orçamento
  estimado); participantes habilitados (documento fiscal/trabalhista/jurídico conferidos —
  estados por tipo); sessão de lances: lance inicial de todos, lances sucessivos decrescentes,
  zona ME/EPP (lance vencedor fora da margem de preferência encerra a zona; dentro → direito de
  preferência registrado; desistência/fora-da-margem documentado); 1 lance = 1 registro
  (idempotente por autor × sequência).
- BR-8.4 Julgamento: classificação automática por proposta; empate → critérios definidos (menor
  prazo, melhor referência) — decisão registrada; adjudicação: ato do ordenador (nome do
  contratado, valor total, itens adjudicados por item); homologação: ato superior
  (perfil distinto do adjudicador — separação de funções), com prazo paramétrico; rejeição de
  proposta (inexequível) registrada por proposta com motivo.
- BR-8.5 Contrato: nasce de homologação; itens espelham os adjudicados (valor unitário ×
  quantidade máxima); valor total derivado (soma) — não editável livremente; assinatura
  eletrônica via GED (S18) quando habilitado, caso contrário assinatura física com GED do
  instrumento; eficácia em data informada; alterações só por aditivo versionado;
  execução: % contratado = Σ fornecimentos ÷ valor (ou quantidade) — teto bloqueia novo
  fornecimento (`CONTRATO_EXECUCAO_TETO`); prorrogação/rescisão com ato e justificativa
  (rescisão: unilateral por inadimplência — com notificação registrada; bilateral por comum
  acordo; impugnação abre processo jurídico).
- BR-8.6 Urgência/emergência (Lei 14.133 art. 75): demanda com urgência exige justificativa
  expandida + aprovação dupla (ordenador + autoridade superior); dispensa de prévia com base
  legal selecionada do catálogo (sem texto livre).
- BR-8.7 Separação de funções: quem solicita não aprova; quem adjudica não homologa
  (perfis distintos do banco; violação → falha nomeada `SEPARACAO_FUNCOES_VIOLADA`).

**Cenários de prova:** demanda emergente sem dupla aprovação bloqueada; zona ME/EPP respeitada;
adjudicação sem homologação não gera contrato; contrato acima do teto de execução não aceita
fornecimento; violações de separação de funções nomeadas.

---

## SPRINT 9 — Compras integradas (2/2): fornecimento → recebimento → estoque/patrimônio → obrigação financeira

**Regras de negócio (além dos contratos da S7):**
- BR-9.1 Fornecimento: criado contra item de contrato vigente (restante disponível > 0);
  quantidade ≤ restante (parcial sempre permitido); valor unitário = do item (variação só com
  justificativa registrada e aprovação); estimativa de entrega (alerta de atraso, sem bloqueio).
- BR-9.2 Recebimento: contra fornecimento; por item recebido: quantidade aprovada,
  rejeitada (motivo), devolvida; rejeição abre prazo de substituição (default 5 dias úteis,
  paramétrico) — desfecho obrigatório (substituído com novo recebimento vinculado, acatado com
  abatimento, ou reprovado com multa) antes do fornecimento fechar; recebimento total
  (aprovado+rejeitado = fornecido) é condição de fechamento; NF vinculada (hash/id fiscal,
  CNPJ do emitente = contratado, valor ≈ aprovado — divergência > tolerância exige justificativa).
- BR-9.3 Destinação: aprovados → ESTOQUE (incrementa saldo via **comando tipado da Sprint 1** —
  reutilização, nunca insert direto) ou PATRIMÔNIO (gera ficha: código sequencial por
  classe, valor registrado, local, responsável, situação DISPONIVEL; patrimônio é bem único —
  1 destino = 1 ficha, jamais duplicado por reprocesso: idempotência por destino).
- BR-9.4 Obrigação financeira: 1 fornecimento fechado com NF → 1 obrigação (valor = aprovado
  com abatimentos líquidos; estado LIQUIDADA aguardando pagamento); reprocessamento do
  fechamento não duplica (backstop único por fornecimento); obrigação registra: contrato,
  centro de custo, competência, fornecedor, documentos.
- BR-9.5 Pagamento (interface com S14/Tesouraria): paga obrigação liquida; pagamento parcial
  permitido (restante permanece liquida); baixa integral encerra; estorno de pagamento
  devolve obrigação a liquida (idempotente) e não afeta recebimento (que é ato distinto —
  princípio da separação de atos patrimoniais).
- BR-9.6 Multas/abatimentos: multa aplicada a contrato (evento com base legal/cláusula, valor,
  desconto da próxima obrigação ou crédito); abatimento de rejeição acatada reduz automaticamente
  a liquidação — tudo em lançamentos separados e somados, nunca edição retroativa de valores.
- BR-9.7 Rastreabilidade completa: item de contrato → fornecimento → recebimento → destino(s) →
  estoque/patrimônio → obrigação → pagamento, com ids cruzados; qualquer ponto da cadeia abre
  a trilha completa (relatório de ponta a ponta por contrato).

**Cenários de prova:** recebimento parcial + rejeição com substituição; NF divergente;
destinação duplicada por reprocesso (idempotente); obrigação única por fornecimento;
pagamento parcial × estorno; trilha ponta a ponta verificável.

---

## SPRINT 10 — Vínculos Jurídico + Convênios/Obras

**Legislação:** Lei 14.133/2016? não — Lei 14.133/2021 (jurídico nas contratações arts. 38/89),
Decreto 9.388/2018 e portarias do Sistema de Convênios (Sucespar-SP/Portal de Convênios),
normas TCU sobre repasses, lei de obras/art. 185 (licitação prévia de projeto básico).

**Regras de negócio:**
- BR-10.1 Consulta jurídica: demanda vinculada a contratação/número/processo (tipo: parecer,
  impugnação, defesa, cobrança); parecer com conclusão (FAVORECIMENTO/IMPROCEDENCIA/CONDICIONADO
  + condicionantes), prazo de resposta, custo honorário; parecer vinculado a processo
  administrativo (S19) e a contratação — decisão condicionada impede o próximo passo
  (`PARECER_CONDICAO_ABERTA`); recurso/segunda instância abre nova consulta filial (a original
  preserva).
- BR-10.2 Contencioso: caso (ativo/passivo), origem (contrato, convênio, trabalhista,
  tributário), instância, valor (exposto/condenação), eventos (movimentos judiciais)
  append-only; desfecho (transação, julgado, desistência) com custas; contrato vinculado ganha
  marcador JURIDICO (bloqueia prorrogação automática enquanto contencioso ativo no objeto).
- BR-10.3 Convênio: instrumento (convocante, convênios-partes, finalidade, valor, vigência),
  fontes (transferência + contrapartida própria % — BR-7.8), plano de trabalho (metas físicas
  e financeiras por período), execução: repasses (data × valor, tipo) e contrapartida aplicada;
  relatórios de execução (financeiro: recursos aplicados; físico: metas cumpridas %) por
  competência paramétrica; prestação de contas só com execução mínima exigida (default:
  aplicada ≥ % paramétrica dos recursos) e sem saldos de execução não justificados;
  termo aditivo (prorrogar/ampliar) versionado; encerramento com termo + saldo final
  contabilizado; inexecução → apuração de responsabilidade (abre contencioso/consulta).
- BR-10.4 Obras: projeto (básico aprovado obrigatório — sem projeto, contrato de obra bloqueia),
  licenças (tipo × situação, validade), contrato de obra (preço global ou unitário), medições
  (físico % medido × financeiro; pagamento proporcional ao medido e aceito — pagamento acima do
  medido bloqueia `OBRA_MEDICAO_INSUFICIENTE`), aditivo quantitativo/financeiro (limites
  paramétricos, justificativa, ato), entrega (comissão de recebimento, ressalvas com prazo de
  sanamento), patrimônio: transferência automática do bem para PATRIMÔNIO na entrega definitiva.
- BR-10.5 Vínculos cruzados: contrato ← convênio (recurso do convênio) ← obra; bloqueios em
  cascata nomeados (encerrar convênio com obra ativa → `CONVENIO_ENCERRAMENTO_BLOQUEADO`);
  toda quebra de vínculo exige justificativa e ato registrado.

**Cenários de prova:** parecer condicionado trava contratação; convênio sem contrapartida não
aprova; obra sem projeto não contrata; pagamento de medição acima do medido bloqueado;
encerramento de convênio com saldo bloqueado.

---

## SPRINT 11 — Homologação integrada + fechamento da matriz por níveis

**Regras de negócio:**
- BR-11.1 Execução das jornadas completas por perfil (admin, coordenação pedagógica,
  operação, docente, tesoureiro/financeiro, jurídico, cidadão quando exposto) em **2 tenants
  de esferas diferentes** (ex.: municipal + estadual) — prova da regra 21 (nada municipal
  hardcoded: regras de limite/calendário/autoridade diferindo por esfera observáveis nos dados).
- BR-11.2 Cenários transversais obrigatórios em todas as novas superfícies: concorrência
  (2 clientes simultâneos em regra de unicidade), reenvio idempotente, estorno repetido,
  cancelamento com efeito, schema ausente (tenant sem módulo instalado), permissão ausente.
- BR-11.3 Matriz de evidência fechada por funcionalidade × nível (IMPLEMENTADO / VALIDADO POR
  TESTES / EXECUTADO EM INTEGRAÇÃO / HOMOLOGADO PELA INTERFACE / BLOQUEADO), com apontamento
  de evidência (file:test / log E2E / screenshot) para cada célula.
- BR-11.4 Relatório de fechamento do incremento + resumo executivo (o que funciona, o que está
  BLOQUEADO e por quê, risco de produção) + prompt do próximo incremento.
- BR-11.5 Documentação operacional: `ULTIMA_EXECUCAO`, `BACKLOG_EXECUTAVEL`, `MATRIZ_FLUXOS_
  VERTICAIS`, `STATUS_REAL_MODULOS` (por níveis, sem promessa) atualizados e coerentes entre si.

---

## SPRINT 12 — RH e Previdência (servidores públicos)

**Legislação:** CF art. 37 (ceilhos) e art. 7 (vacations/13°), Lei 8.112/1990 (regime dos
federais — referência de prazos), LC 51/84 (INSS) e regimes próprios (RPPS) por esfera,
eSocial — eventos do setor público (S-1000/S-1005/S-1200…), Censo dos Servidores.

**Regras de negócio:**
- BR-12.1 Cadastro: cargo (classe/Padrão/Nível), posição, servidor (matrícula funcional única
  por tenant, vínculos — 1 vínculo ativo por servidor, estágio/cargo em comissão marcado),
  remuneração por item (natureza paramétrica: vencimento, adicionais, gratificações).
- BR-12.2 Folha mensal: cálculo determinístico (base + incorporações − descontos; INSS/RPPS por
  faixa/alíquota paramétrica por regime; IRRF **tabela progressiva por ano** + acumulação
  mensal; contribuição previdenciária sobre base certa por natureza do item); fechamento da
  competência = **1 folha por servidor × competência** (backstop único); reprocessamento =
  nova versão que veta a anterior (ambas auditáveis); folha fechada não aceita lançamento
  retroativo sem estorno com justificativa.
- BR-12.3 Férias: aquisição automática por ciclo aquisitivo (12 meses de serviço → 30 dias
  paramétricos); gozo programado (aviso com antecedência mínima paramétrica, default 30
  dias — art. 84 Lei 8.112), pagamento com adicional de 1/3 (separado e auditável),
  abono pecuniário (pedaços) quando autorizado; saldo negativo bloqueado; gozo parcial
  (fracionado em até 3 períodos — regra paramétrica).
- BR-12.4 Afastamentos: doença (RGPS: benefício pago pela previdência — servidor continua
  folha com abatimento; RPPS próprio: regime paramétrico; opção RGPS: 1/3 de vencimento na
  licença por 3+ meses — paramétrico), estudo, maternidade (120 dias CF art. 7 XVIII;
  paternidade 5 dias; adoção conforme CF), saúde — cada evento com documentos (GED) e
  devolução automática ao fim do prazo (aviso antes do término).
- BR-12.5 13º salário: 1/12 por mês de serviço ≥ 15 dias (CF art. 7 VIII); primeira parcela
  até 30/11 e segunda até 20/12 (datas paramétricas); acúmulo anual; reflexos em férias/FGTS-
  equivalentes calculados automaticamente.
- BR-12.6 Subsídio/limite constitucional: teto por esfera (município: vencimento do Governador
  do estado; estado: Ministro do STF; federais: próprios) — folha com servidor acima do teto
  gera alerta e bloqueio no fechamento (`SERVIDOR_ACIMA_TETO`); subsídio (unificado) vs
  vencimento+adicionais é modo por cargo, não misturável.
- BR-12.7 Contracheque: versionado por competência, LGPD (acesso só do servidor + RH),
  demonstração do cálculo (cada valor com fórmula/componente).
- BR-12.8 eSocial público: exportação dos eventos (tabelas S-1000/S-1010; S-1005 admissão/
  S-1006 movimentação; S-1200 remunerações por evento) em lote versionado e idempotente por
  competência (reprocesso reenvia o mesmo lote — sem duplicar); validação de conformidade
  pré-envio (mensagens de validação listadas, bloqueio quando erro).

**Cenários de prova:** folha fechada 2× idempotente; IRRF acumulado confere mês a mês; férias
com abono e aviso insuficiente; afastamento que estende e devolve; teto bloqueia fechamento;
lote eSocial reprocessável.

---

## SPRINT 13 — Orçamento e Responsabilidade Fiscal (LRF)

**Legislação:** Lei 4.320/1964 (elaboração/execução orçamentária), LC 101/2000 (LRF — arts.
20, 26, 32, 51, 54), Lei 9.394/96 e CF art. 212 (25% da receita de impostos para manutenção e
desenvolvimento do ensino — MDE, por esfera), Lei Orgânica/Leis estaduais por jurisdição
(parametrizadas).

**Regras de negócio:**
- BR-13.1 Estrutura: função → subfunção → programa → ação (projeto/atividade/subprojeto);
  dotação por ação × exercício × fonte de recurso (recursos próprios, transferões correntes,
  vinculações — MDE/FUNDEB como fontes com restrição de uso paramétrica: ex. MDE não pode
  financiar pessoal? — sim: MDE **pode** financiar pessoal, FUNDEB 70% é pessoal; restrição
  por fonte é catálogo configurável); créditos adicionais (suplementar/vinculado/especial/
  extraordinário — Lei 4.320 art. 41; anulatórios automáticos/sugestionados, aprovação por ato).
- BR-13.2 Disponibilidade = dotação inicial + créditos + rollover? (sem rollover — exercício
  anual; restos a pagar tratados como compromisso do próximo, com classificação
  "empenhado/liquidado" do exercício anterior — RP processado × RP não processado).
- BR-13.3 Empenho (CP): só com disponibilidade ≥ valor (excedente → `EMPENHO_ORCAMENTO_
  INSUFICIENTE`), só para ação × fonte compatível com o objeto (restrições de MDE/FUNDEB
  conferidas); parcelado com total travado; anulação/redução de empenho (justificativa;
  redução só até o já empenhado); empenho cancelado devolve disponibilidade.
- BR-13.4 Liquidação (SL) ≤ empenho disponível (excesso → `LIQUIDACAO_SUPER_EMPENHO`);
  pagamento (SP) ≤ liquidado; trilha CP → SL → SP por empenho (relatório por ação × fonte ×
  mês); saldos (disponível = dotação − CP) sempre derivados, nunca armazenados livres.
- BR-13.5 Limites LRF monitorados **por esfera** (regra 24): despesas de pessoal (executivo
  municipal 50% da RCL, do qual 25% educação+cultura; estadual 60% total/50% educação? —
  tabela parametrizada: esfera × órgão × % limite × base de cálculo (RCL)); superávit de
  execução bimestral com tendência de estouro → alerta → **bloqueio paramétrico** de novos
  empenhos de despesa não continuada (`LRF_EMPENHO_BLOQUEADO` — continuada exige justificativa
  + decreto de contingência registrado); dívida bruta ≤ teto (120 dias da RCM — LC 101 art. 26
  com exceções paramétricas).
- BR-13.6 Previsão bimestral (LC 101 art. 51): relatório gerado automaticamente com dados de
  execução + compromissos (empenhos não liquidados) + fluxo esperado; posicionamento
  (compatível/estouro) com os números; publicação no portal (S16).
- BR-13.7 RREO (LC 101 art. 54): semi-anual e anual; conjuntos mínimos (receita orçada ×
  realizada, dívida, restos, despesas comprometidas/liquidadas/pagas, limitados por categoria,
  posição nos limites); gerado de consulta (origem verificável); versão fixa por corte
  (imutável; retificação = nova versão); exportação no formato do TCE paramétrico por esfera.
- BR-13.8 Competência fechada (exercício encerrado) bloqueia empenho/liquidação nova
  (estorno apenas); relatório de encerramento (RP por natureza) alimenta o próximo exercício.

**Cenários de prova:** empenho acima da disponibilidade bloqueado; crédito suplementar com
anulação confere; MDE financiado por fonte não MDE e vice-versa; bloqueio LRF disparado pela
previsão bimestral; RREO confere com a trilha CP/SL/SP.

---

## SPRINT 14 — Tesouraria e Contabilidade (NBC TSP)

**Legislação:** NBC TSP (entidades do setor público — equivalência IFRS), Lei 4.320 (contas),
LC 101 (divulgação), normas do Ministério da Fazenda (RGF municipal/estadual), ICMS/ISS para
receitas próprias, LGPD (dados bancários/servidores).

**Regras de negócio:**
- BR-14.1 Plano de contas: árvore (patrimônio, passivos, resultado, receitas, orçamento) com
  natureza (contábil/orçamentária) — contas de lançamento vs de síntese; mapa contábil ×
  orçamentária (1 item orçamentário ↔ conta contábil, configurável).
- BR-14.2 Lançamento contábil: **débitos = créditos sempre** (saldo do lançamento = 0),
  sem valor zero por linha, contas válidas (existem, são de lançamento, abertas); período
  (competência) aberto; lançamento gerado por origem (recebimento, folha, convênio,
  provisão) carrega a referência — **reprocessamento da origem não duplica o lançamento**
  (1 origem + evento = 1 conjunto; backstop único por referência de origem).
- BR-14.3 Caixa/bancos: conta por instituição/agência/saldo × **fonte de recurso** (fontes
  vinculadas — FUNDEB — com restrição de uso conferida: uso em categoria vedada → falha
  nomeada); saldo derivado de lançamentos (conciliação é verificação, não ajuste livre —
  ajuste de diferença só por lançamento de encerramento com justificativa).
- BR-14.4 Recebimento (tesouraria): título (imposto próprio, taxa, convênio repasse,
  FUNDEB) com vencimento e parcelas; pagamento parcial permitido; baixa idempotente por
  comprovante (hash/ID do documento); título vencido → régua de cobrança (alerta paramétrico);
  inscrição em dívida ativa (comando, gerando título de dívida ativa com processo — S19).
- BR-14.5 Conciliação bancária: importação assistida (OFX/CSV/CNAB) com **pré-validação**
  (linhas duplicadas por hash, mapeamento faltante, totais vs extrato) — preview obrigatório
  antes de aplicar; aplicação grava extrato versionado + marcações de reconciliação
  idempotentes; reprocessar o arquivo não duplica linha nem marcação.
- BR-14.6 Pagar: obrigações liquidadas (S9/S12 folha) → fila de pagamento por data/conta;
  pagamento grava saída (fonte conferida) e baixa a parcela; estorno idempotente;
  pagamento com fonte errada detectado na conferência (bloqueio pré-pagamento).
- BR-14.7 Fechamento de competência: encerra o período (bloqueia lançamentos novos;
  estornos continuam abertos com justificativa); gera balancetes (patrimônio, resultado,
  fluxo de caixa por fonte) versionados; RGF mensal (dataset do Ministério da Fazenda por
  esfera) gerado do balancete com origem verificável.
- BR-14.8 Provisões: contas a pagar estimadas (multas, licenças) — provisionamento por
  lançamento (provisiona → realiza: estorno da provisão + gasto real; diferença registrada).

**Cenários de prova:** lançamento desbalanceado bloqueado; reprocessamento de origem →
1 conjunto de lançamentos; conciliação reimportada → sem duplicata; fonte FUNDEB em gasto vedado;
fechamento bloqueia lançamento e estorno funciona.

---

## SPRINT 15 — Escolar completo: matrícula, turmas, calificação acadêmica, histórico e portabilidade

**Legislação:** LDB (Lei 9.394/1996 — arts. 22–32), FNDE (SisAluno, MROSD — modelo de dados),
INEP (Censo Escolar), LGPD (dados de menores: responsável), Lei 13.415/2016 (horas letivas),
calendário escolar (200 dias/LDB art. 24 — paramétrico por esfera/jurisdição).

**Regras de negócio:**
- BR-15.1 Matrícula: aluno + responsável (LGPD — responsável obrigatório para menores; dados
  sensíveis opcionais e sinalizados); unidade × turma × turno × ano letivo; **1 matrícula
  vigente por aluno × unidade × ano letivo** (backstop único; matrícula ativa em 2 unidades
  do mesmo tenant no mesmo ano → `MATRICULA_VINCULOS_CONCURRENTES` — multi-unidade só
  inter-redes com trânsito registrado); vaga: turma com capacidade (matrículas ≤
  capacidade — excesso exige justificativa + autorização; turma sem vaga → `TURMA_SEM_VAGA`).
- BR-15.2 Turma: capacidade, turno, ano letivo, componentes curriculares com docente
  responsável (atribuição por componente × turma × período — 1 docente por combinação);
  reorganização de turma (migrar aluno dentro do ano) gera evento, preserva histórico e
  confere vagas nos dois lados atomicamente.
- BR-15.3 Calendário acadêmico: períodos de ensino (nomes livres — "1º bimestre", "1º
  semestre", "semestral"), datas (abertura/encerramento/fechamento de notas), feriados
  parametrizados por jurisdição (nacionais + estaduais + municipais — tabela por UF/município/
  unidade), dias letivos conferidos (meta paramétrica, default 200); período só fecha com
  todas as notas de todos os alunos entregues (pendências listadas nominalmente);
  fechamento trava notas (retificação posterior exige reabertura justificada + auditoria).
- BR-15.4 Frequência: lançamento por aluno × dia (presente/faltante justificada/
  injustificada/afastado) ou por turma com exceções; frequência % por período calculada;
  **abaixo do piso paramétrico (default 75%) → marca "em risco de reprovação" automática**
  (não reprova sozinha — decisão docente, registrada); frequência de afastamento legal
  (S12) não conta como falta.
- BR-15.5 Notas: escala paramétrica por escola (0–10 decimal, qualitativa, conceitos) —
  domínio por unidade (mesma escola, mesma escala; troca de escala só em novo ano letivo);
  nota por componente × turma × aluno × período × tentativa (recuperação = 2ª tentativa —
  a nota final é a melhor ou a regra da escola, paramétrico); notas fechadas (período
  encerrado) imutáveis; consolidação: média final por componente → situação final
  (APROVADO / REPROVADO / EM_RECUPERACAO / TROCA_DE_ANO — regra por nível de ensino
  paramétrico: fundamental não tem reprovação por nota em alguns regimes — configuração).
- BR-15.6 Histórico escolar: boletim por período **versionado e imutável** (1 versão por
  período × aluno; reemissão = mesma versão; retificação pós-fechamento = nova versão com
  justificativa e o histórico mostra a evolução das versões); histórico acumulado por aluno
  (períodos, unidades, frequências, situações, transferências) — consulta única e
  auditável; certidão digital (PDF/hash) com código de verificação.
- BR-15.7 Trânsito/portabilidade: saída (unidade emissora: situação atual, frequência, notas
  fechadas) + entrada (unidade receptora no mesmo tenant) em **uma operação atômica** —
  matrícula de origem encerrada + nova criada com histórico vinculado; imutabilidade: o
  fragmento transferido não muda (cópia referenciada, não referência); rejeição da entrada
  devolve sem efeito (transação).
- BR-15.8 Evasão/trancamento: encerramento de matrícula com motivo (transferência, evasão,
  óbito, inatividade) e data; evasão alimenta indicador (aba de desistência por unidade/
  turma — S20); reativação de trancado só no mesmo ano letivo com justificativa.
- BR-15.9 Alinhamento censitário: comparador matrícula × lote Educacenso (S5): divergências
  (aluno censizado não matriculado e vice-versa) listadas e alimentam o lote como
  inconsistência; MROSD: exportação do modelo padrão (aluno, responsável, matrícula, turma,
  componente) em lote versionado idempotente.

**Cenários de prova:** matrícula duplicada bloqueada; turma sem vaga; período não fecha com
nota pendente; frequência < 75% marca; boletim imutável pós-fechamento; trânsito atômico 2
unidades; comparador censitário aponta divergência.

---

## SPRINT 16 — Portal de Transparência e LAI

**Legislação:** LC 101/2000 art. 58 (dados mínimos), Lei 12.527/2011 (LAI — prazos: 20 dias
+ 10 de prorrogação única, art. 11; recurso art. 15 — 10 dias), Decreto 7.185/2010,
Resolução TCU 277/2005, LGPD (anonimização), Lei 14.133 art. 71 (transparência da
contratação — PNCP: publicação obrigatória no Portal Nacional de Contratações Públicas).

**Regras de negócio:**
- BR-16.1 Dataset de transparência: catálogo por tipo (orçamento, execução orçamentária,
  receitas, despesas por ação, contratos e aditivos, pagamentos de pessoal por unidade,
  empenhos, convênios, obras, atos normativos, limites de pessoal, dívida) — cada item
  definido por (consulta/fonte + frequência de atualização + responsável); **publicação só
  por job agendado ou comando tipado** (manual livre veta); cada publicação carrega
  (origem, consulta, timestamp, versão) — visível no portal como metadado ("dados de
  {consulta} gerados em {timestamp}").
- BR-16.2 Publicações versionadas: nova geração não apaga a anterior (histórico por corte);
  dados corrigidos → nova versão com nota de retificação; publicação com erro → recolhida
  (motivo) e republicada (a recolhida fica marcada, não apagada).
- BR-16.3 PNCP: contratos/licitações/publicações obrigatórias (edital, aviso, resultado,
  homolgação, contrato, aditivo) espelhados no formato PNCP (publicação no portal externo —
  integração paramétrica por esfera: PNCP nacional ou portal estadual equivalente).
- BR-16.4 Pedido LAI: protocolo único (sequência por ano), solicitante (nome/endereço/
  documento — LGPD: uso restrito), objeto, canal (web/balcão); triagem (unidade
  competente — encaminha quando não é a detentora, registrando o encaminhamento); análise
  com **prazo corrido em dias úteis** (20 + prorrogação única de 10 com motivo — alertas em
  5 dias úteis de antecedência); resposta (integral/parcial/negada — negativa com fundamento
  legal; parcialmente deferida com justificativa por recusa); resposta pode ser
  "documentação anexada" (GED) ou "divulgada no portal" (aponta a publicação); prazo
  cumprido ou não (métrica).
- BR-16.5 Recurso: 10 dias para interpor (após ciência), decisão pelo órgão recursal
  (superior ao que respondeu) mantendo/modificando/negando — protocolo vinculado ao
  original (nunca perde o trilho); decisão final é terminativa (novo recurso → via
  administrativa superior ou controle — registro apenas).
- BR-16.6 Anonimização padrão: resposta pública remove/oculta dados pessoais (nome completo
  → "S.C.S.", CPF com máscara, endereço → cidade) salvo interesse público documentado
  (ex.: dirigente em transparência de pessoal — regra por tipo de dado); revisão pelo
  responsável antes de publicar (workflow: análise → revisão → publicação).
- BR-16.7 Métricas de atendimento: tempo médio, % dentro do prazo, recusas por motivação,
  recorrentes por tema — dashboard do gestor de informação (DPO — S17).

**Cenários de prova:** prazo LAI contando dias úteis (fim de semana não conta); prorrogação
única; recurso reverte decisão; publicação com metadado de origem; recolhida e republicada;
anonimização aplicada por tipo de dado.

---

## SPRINT 17 — LGPD & Segurança de Dados (multi-jurisdicional)

**Legislação:** LGPD (Lei 13.709/2018), ANPD (resoluções), GDPR (para exportação/EU —
equivalências), Marco Civil, normas do TCU sobre dados (TC 002.223), ICP/infra.

**Regras de negócio:**
- BR-17.1 Funções: DPO (encarregado — 1 por tenant, perfil específico), titular (servidor,
  aluno/responsável, cidadão, contratado), operador (sistema — registros de tratamento).
- BR-17.2 Base legal por finalidade: catálogo (consentimento, obrigação legal, execução de
  programa, legítimo interesse, pesquisa) — todo dado pessoal coletado declara a base
  (cadastro de tratamento: finalidade × base × dados × retenção × compartilhamento);
  cadastro de tratamento versionado (mudança de finalidade exige nova revisão DPO).
- BR-17.3 Direitos do titular: acesso (consulta completa do titular em JSON/CSV com metadado),
  retificação (campos retificáveis com trilha antes/depois), portabilidade (exportar no
  formato nativo), anonimização/oclusão/extinção (extinção = **anonimização irreversível dos
  campos pessoais, preservando chaves estatísticas** — PK nunca é destruída; dados de
  menor de idade sob guarda do responsável), oposição (cessar uso além da base declarada),
  revogação de consentimento (efeitos calculados: o que para de fluir × o que continua por
  obrigação legal — resposta ao titular lista os dois conjuntos); pedidos com workflow
  (protocolo, prazo 15 dias LGPD art. 19? — resposta em 15 dias úteis, paramétrico;
  decisão; recurso interno).
- BR-17.4 Classificação e acesso: campo × nível (público, interno, confidencial, sensível —
  saúde/raça/credo); nível sensível exige base específica e consentimento quando aplicável;
  máscara por perfil (CPF/telefone/endereço/saúde — exibição sempre mascarada no listing,
  plena só no detalhe com permissão); acesso a dado confidencial/sensível gera **trilha
  individual** (quem, quando, qual registro, qual campo) — append-only.
- BR-17.5 DPIA (AIPD): por fluxo novo que trate sensíveis (saúde escolar, transporte de
  menores, biométricas); etapas (identificação de riscos × impactos × mitigações), revisão
  periódica (anual ou por mudança), aprovação DPO; fluxo sem AIPD aprovado bloqueia captura
  de sensível (flag no cadastro de tratamento).
- BR-17.6 Retenção: política por tipo (retenção em anos ou até evento, destino:
  conservar/anonimizar/arquivar — tabelas paramétricas por jurisdição); **job de varredura
  com dry-run** (lista o que atingiu a política + preview do destino) e aplicação
  confirmada (lote auditável); varredura nunca apaga PK (anonimiza).
- BR-17.7 Incidente: registro de incidente (vazamento/acesso indevido/perda) com (data,
  escopo, titulares afetados, causa, ações, comunicação ANPD paramétrica — prazo 3 dias
  úteis para risco relevante, comunicável), trilha de tratamento até encerramento;
  notificação de titulares (template + envio registrado) quando exigido.
- BR-17.8 Multi-país: catálogo jurídico por país (BR=LGPD, EU=GDPR, paramétrico) — prazos,
  bases legais, papel do encarregado, portabilidade no formato; tenant declara país/jurisdição
  → motor usa o catálogo sem código específico (regra 24 estendida a fronteira).

**Cenários de prova:** pedido de acesso com metadado; extincão anonimiza sem perder estatística;
revogação lista efeitos; AIPD ausente bloqueia sensível; incidente com prazos; GDPR tenant
usa prazos GDPR.

---

## SPRINT 18 — GED avançado (documentos, integridade, assinatura, retenção)

**Referência:** MP 2.200-2/2001 (assinatura eletrônica — níveis: simples, avançada,
qualificada/ICP-Brasil), ICP-Brasil (ACs), Arquivologia (planos de classificação/ATA —
tabelas de guarda), Lei 14.063/2020 (gestão documental na AP Federal — referência).

**Regras de negócio:**
- BR-18.1 Documento: tipo (contrato, edital, ato, NF, certidão, foto, laudo…), vinculação a
  processo/registro (contrato S8, recebimento S9, LAI S16, processo S19, matrícula S15 —
  referência genérica por tipo+id), metadados obrigatórios por tipo (catálogo de tipos com
  campos e regras).
- BR-18.2 Versões: upload cria versão; 1 versão vigente por documento; nova versão veta a
  anterior (a anterior mantém hash e estado "suplantada"); download retorna a versão
  informada (default vigente) com hash — **verificação de integridade**: hash SHA-256
  recalculado e conferido; divergência → `GED_INTEGRIDADE_VIOLADA` (bloqueia uso do
  documento em atos — ex.: contrato com integridade violada bloqueia fornecimento).
- BR-18.3 Assinatura: eventos append-only encadeados — cada assinatura assina (hash do
  documento × hash do evento anterior) formando corrente; assinatura digital (certificado
  ICP-Brasil: validade, cadeia, finalidade — conferidas no ato; assinatura qualificada
  habilita atos formais) ou por senhas fortes com 2FA (assinatura avançada interna —
  declarada, nunca iguala a qualificada em ato formal); verificação da corrente completa
  (qualquer quebra de link histórica → violação detectada); assinantes múltiplos (ordem
  opcional; ato formal exige todos os obrigatórios antes de viger).
- BR-18.4 Ciclo: RASCUNHO → EM_ASSINATURA → ASSINADO/VIGENTE → ARQUIVADO; arquivamento
  exige (vigência encerrada OU termo de encerramento) + todos os signatários obrigatórios
  assinados; eliminação física só após fim do prazo de guarda do tipo **e** sem vínculo
  vigente (contrato ativo bloqueia eliminação — `GED_RETENCAO_BLOQUEADA`).
- BR-18.5 Tabelas de guarda (ATA paramétricas por tipo × jurisdição): prazo em anos/evento
  + destino (conservação permanente, arquivagem, eliminação); job de varredura (dry-run
  first) sugere transições; decisão de eliminação é ato registrado (responsável + motivo +
  ata de eliminação gerada).
- BR-18.6 Busca: textual (indexada) + metadados + OCR (integração paramétrica por
  capacidade); resultados mostram (documento, versão, página/seção, vínculo) e abrem o
  trilho; permissão segue o vínculo (acesso ao documento respeita o acesso ao processo/
  contrato dono).
- BR-18.7 Relação com módulos existentes: `educacao_biblioteca_digital_referencia`
  (ged_documento_id) passa a validar existência e estado do GED; certidão do histórico
  escolar (S15) é documento GED assinado; contrato (S8) só vige com GED assinado quando o
  modo de assinatura do órgão for eletrônico.

**Cenários de prova:** upload de nova versão vetanta a anterior; tamper detectado no load;
corrente de assinatura quebrada historicamente; eliminação bloqueada com contrato ativo;
ata de eliminação gerada.

---

## SPRINT 19 — Processos Administrativos e Workflow (e-PAP)

**Referência:** Lei 14.133 art. 128 (processo único da contratação), Lei 14.133 arts.
129–130 (instrumento de formalização), SIAFI/e-Pap (padrões federais), Lei 9.784/99
(processo administrativo federal — referência de prazos/decisões).

**Regras de negócio:**
- BR-19.1 Processo: número único por unidade (sequência por ano, formatado `AAAA.NNNNN.UF`
  paramétrico), assunto, área, interessado(s), unidade responsável, classificação
  (confidencialidade — S17); abertura por comando ou vínculo automático (demanda aprovada —
  S8; pedido LAI — S16; convênio — S10); 1 processo agrrega (documentos GED, registros
  vinculados, peças, despachos, eventos).
- BR-19.2 Etapas: sequência definida por fluxo (catálogo de fluxos por tipo de processo:
  análise técnica → jurídico → ordenador → autoridade), 1 responsável por etapa (atribuição
  explícita — nunca "todos"); prazo por etapa (paramétrico por fluxo); ações por etapa
  (DEFERIR / INDEFERIR / DEVOLVER (motivo + para quem) / ENCAMINHAR (próxima/outra) /
  EXARAR DESPACHO (registro sem mover) / ARQUIVAR (final)); etapa avança **somente por ação
  tipada** — despacho não avança; devolução reabre a etapa de origem mantendo o trilho.
- BR-19.3 Delegação e substituição: delegação (por período + escopo — atos da delegação
  assinam "no exercício da delegação", trilha registra ambos); substituição automática por
  ausência (ordem paramétrica); ninguém decide sobre ato de si mesmo (auto-atribuição
  bloqueada quando a regra de conflito exige — separação de funções S8).
- BR-19.4 Peças e documentos: anexo = versão GED (inteiro, não extraído); extração
  (referência a trecho) registrada com citação; peça é imutável (correção = nova peça
  "retifica a peça N"); ordem cronológica única (timestamps do banco).
- BR-19.5 Cronologia: timeline append-only de todos os eventos (abertura, atribuição, ação,
  despacho, vínculo, publicação) com ator/data/etapa — consulta pública ao processo segue a
  permissão do processo (processo de contratação: acesso conforme LAI/PNCP paramétrico).
- BR-19.6 Prazos e alertas: atraso por etapa gera alerta (paramétrico) e marca o processo
  "em atraso"; contagem em dias úteis quando a regra da jurisdição for assim (catalogo);
  decisão final (última etapa) encerra: efeito (deferido/indeferido) + publicação opcional
  (transparência S16) + notificação ao interessado (canal paramétrico).
- BR-19.7 Transferência entre unidades: memorando (motivo, data) — histórico completo vai
  junto (nunca cópia parcial); unidade receptora continua do estado atual; processos de
  outra esfera: transferência inter-tenant permitida só entre unidades do mesmo grupo
  corporativo (regra de grupo S20) com ato registrado.
- BR-19.8 Arquivamento: decisão final + prazo de inércia paramétrico → arquivado (imutável,
  consultável); desarquivamento só por decisão registrada (reabertura — novo evento, trilho
  completo).

**Cenários de prova:** processo único da contratação agrega todos os atos S8; etapa sem ação
tipada não avança; delegação auditável; transferência preserva cronologia; atraso gera
alerta; arquivado imutável.

---

## SPRINT 20 — BI consolidado, gestão multi-unidade e prontidão de exportação

**Regras de negócio:**
- BR-20.1 Grupo corporativo: tenant pai (prefeitura/governo de estado/ministério) agrega
  unidades (secretarias, escolas, hospitais, autarquias, superintendências — regra 22);
  hierarquia administrava (órgão superior) com drilldown no mapa; dashboards do grupo
  agregam por unidade com perfis: SuperAdmin (grupo inteiro), Gestor (escopo da unidade),
  Fiscalizador (somente leitura).
- BR-20.2 KPI com origem: todo indicador da tela carrega (nome, fórmula/texto, consulta ou
  snapshot, timestamp, granularidade) — **zero ≠ indisponível**: sem dados mostra 0 com
  contexto ("nenhum lançamento nesta competência"); schema ausente mostra código
  nomeado com ação; agregado nunca vem de coleção mock (regra 12) — sempre de consulta ou
  snapshot versionado.
- BR-20.3 Snapshots versionados: relatórios pesados (RREO, censo, comparativos) geram
  snapshot (id, parâmetros, dados, timestamp) — imutável; nova geração = novo snapshot;
  drilldown parte do snapshot (o que foi visto é o que se abre).
- BR-20.4 Comparativos multi-unidade: métricas por unidade (custo merenda por aluno,
  conformidade FUNDEB, taxa de aprovação, execução orçamentária, atraso LAI, inadimplência
  transporte) com ordenação/benchmark (mediana do grupo) — para priorização do gestor.
- BR-20.5 Processamento assíncrono: fila por tarefa (relatório grande, import, export,
  publicação) com **estado persistido** (PENDENTE/RODANDO/SUCEDIDO/FALHOU/CANCELADO + logs +
  retries); reprocesso idempotente (1 tarefa = 1 resultado; retry não duplica efeito);
  cancelamento coopertivo; visível ao usuário (progresso estimável) e ao admin (fila do
  tenant).
- BR-20.6 Padrões de saída (export): MROSD/INEP, RREO (formato TCE paramétrico), PNCP,
  transparência (dataset), eSocial — cada export é lote versionado (id, parâmetros, arquivo,
  hash) e reprocessável sem duplicar efeitos externos (idempotência por lote × alvo).
- BR-20.7 Internacionalização (países de exportação): camada i18n (pt-BR base; en/es
  paramétricas por tenant), moeda e formatação (padrão por país), calendário/feriados por
  jurisdição (S15/16 usam o catálogo), regras jurídicas por país (S17 usa o catálogo
  jurídico) — **zero regra hardcoded por país**: todo comportamento deriva do catálogo
  (país × esfera × parâmetro).

**Cenários de prova:** KPI com origem e zero≠indisponível; drilldown de snapshot; tarefa
assíncrona com retry idempotente; comparativo multi-unidade; tenant "export" com idioma/
moeda/catalogo jurídicos alternados sem código especial.

---

## SPRINT 21 — Aceitação global, produção e próximos incrementos

**Regras de negócio:**
- BR-21.1 Suíte E2E global: todas as jornadas (educação + compras + financeiro + RH +
  transparência + GED + processos) executadas por perfil em 2 tenants de esferas distintas;
  cenários de concorrência/reenvio/estorno em toda superfície nova; resultado 100% do
  total declarado (blocos por sprint somados).
- BR-21.2 Banco do zero: `script_completop.sql` aplica em instância limpa + migrations
  incrementais chegam ao mesmo estado (checksum de schema comparado); backup/restore
  executado e validado (restore → health checks → 1 jornada fumega).
- BR-21.3 Desempenho: carga declarada (X usuários concorrentes por perfil-alvo) com p95
  medido e documentado por endpoint crítico (folha, empenho, matrícula, portal); gargalo
  encontrado documentado com remediação agendada (não prometer o que não roda).
- BR-21.4 Produção: checklist (credencial de produção — adiada deliberadamente até aqui —
  plano de corte/rollback/monitoramento/DR; `.env.example` completo; documentação de deploy
  atualizada); credencial criada e go-live apenas com aprovação explícita (este ciclo fecha
  com o plano pronto, não com a chave virada).
- BR-21.5 Matriz final por níveis (funcionalidade × IMPLEMENTADO/VALIDADO POR TESTES/
  EXECUTADO EM INTEGRAÇÃO/HOMOLOGADO PELA INTERFACE/BLOQUEADO) com evidência apontada;
  relatório executivo de fechamento do Ciclo 2 (escopo cumprido, o que ficou BLOQUEADO e
  por quê, riscos, investimento seguinte).
- BR-21.6 Próximos incrementos (documentados, não prometidos): Saúde (atenção primária,
  vigilância sanitária — base ACS já existente), Assistência Social (SUAS — base já
  existente), Urbano/Obras completas, Legislativo (matérias, sessões — base Bloco 8),
  Indústria 360 (base já existente), integração Compras.gov.br/NF-e nativa, mobilidade
  (app cidadão).

---

## Sequência, dependências e gates

| Sprint | Título | Depende de | Migration? |
|---|---|---|---|
| 1 | Merenda — integridade transacional | — | sim (coluna + índices + dedupe) |
| 2 | Estados por recurso, bypass, tempo único | 1 | possivelmente (versões) |
| 3 | Biblioteca completa | 2 | sim (ocorrências/inventário) |
| 4 | Transporte — da rota à viagem | 2 | sim (viagem/veiculo/motorista/ocorrência) |
| 5 | FUNDEB/custos/rateio + magistério + censo | 1–4 | sim (rateio/avaliação) |
| 6 | Jornadas pelas telas reais | 1–5 | não (UI) |
| 7 | Mapa da frente administrativa | 1–6 | seed de permissões |
| 8 | Compras 1/2 — demanda→contrato | 7, 19 (processo parcial) | sim |
| 9 | Compras 2/2 — fornecimento→obrigação | 8, 1 (estoque) | sim |
| 10 | Jurídico + Convênios/Obras | 8 | sim |
| 11 | Homologação integrada + matriz | 1–10 | não |
| 12 | RH e Previdência | 11 | sim |
| 13 | Orçamento e LRF | 12, 9 | sim |
| 14 | Tesouraria e Contabilidade (TSP) | 13, 12 | sim |
| 15 | Escolar completo (matrícula/notas/trânsito) | 2, 6 | sim |
| 16 | Transparência e LAI | 5, 8, 12, 14, 19 | sim |
| 17 | LGPD e Segurança de Dados | 15, 16 | sim |
| 18 | GED avançado | 9, 15, 16 | sim |
| 19 | Processos Administrativos (e-PAP) | 16, 18 | sim |
| 20 | BI consolidado + multi-unidade + i18n | 12–19 | sim (snapshots/fila) |
| 21 | Aceitação global + produção | 1–20 | não |

Observação de ordem: o processo administrativo (S19) é usado pela S8 (processo único da
contratação) — na S8 o fluxo mínimo de processo é implementado junto (comando de abertura +
vinculação), ficando o motor completo na S19; a S7 (mapa) já define esse contrato.

**Gates de cada sprint (idênticos, cumulativos):** build `-warnaserror` 0w/0e · suítes
existentes verdes (extendidas) · E2E Tier-2 baseline + blocos novos, 100% · migration ×2 +
manifest + 6 consolidados (quando aplicável) · falhas nomeadas · relatório + docs de
execução · RC própria `RC-EVO Ciclo 2 Sprint N: ...`.
