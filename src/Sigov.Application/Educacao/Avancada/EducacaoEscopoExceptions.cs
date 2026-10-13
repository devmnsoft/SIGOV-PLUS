namespace Sigov.Application.Educacao.Avancada;

// Falhas nomeadas de escopo institucional (Ciclo 1, item #6): escola/ano letivo/contexto
// devem pertencer ao mesmo tenant e à mesma entidade do contexto autenticado antes de
// qualquer vínculo ser gravado. Mensagem segue o padrão de código nomeado do módulo
// (regra 13: falha explícita; regra 24: nada municipal hardcoded — a entidade é o escopo).

/// <summary>Escola inexistente para o tenant atual ou excluída logicamente (HTTP 404, código ESCOLA_INEXISTENTE).</summary>
public sealed class EscolaInexistenteException(long EscolaId)
    : Exception($"ESCOLA_INEXISTENTE: escola {EscolaId} não encontrada em sigov.escola para o tenant atual (ou excluída logicamente).");

/// <summary>Escola de outra entidade do contexto institucional (HTTP 409, código ESCOLA_FORA_DO_ESCOPO).</summary>
public sealed class EscolaForaDoEscopoException(long EscolaId, long EscolaEntidadeId, long ContextoEntidadeId)
    : Exception($"ESCOLA_FORA_DO_ESCOPO: escola {EscolaId} pertence à entidade {EscolaEntidadeId} e o comando está no contexto da entidade {ContextoEntidadeId}; vínculos não podem cruzar entidades.");

/// <summary>Ano letivo ausente para o tenant/entidade (e escola quando informada) (HTTP 422, código ANO_LETIVO_INEXISTENTE).</summary>
public sealed class AnoLetivoInexistenteException(int AnoLetivo)
    : Exception($"ANO_LETIVO_INEXISTENTE: não existe ano letivo {AnoLetivo} em sigov.ano_letivo para o tenant/entidade do contexto; cadastre o ano letivo ou corrija o parâmetro.");

/// <summary>Registro de outra entidade do contexto institucional (HTTP 409, código CONTEXTO_INSTITUCIONAL_DIVERGENTE).</summary>
public sealed class ContextoInstitucionalDivergenteException(string Recurso, long Id, long LinhaEntidadeId, long ContextoEntidadeId)
    : Exception($"CONTEXTO_INSTITUCIONAL_DIVERGENTE: registro sigov.{Recurso}/{Id} pertence à entidade {LinhaEntidadeId} e o comando está no contexto da entidade {ContextoEntidadeId}; troque de contexto institucional antes de operar.");

/// <summary>Produto inexistente/inativo para o tenant atual na criação de cardápio (HTTP 404, código CARDAPIO_PRODUTO_INEXISTENTE).</summary>
public sealed class CardapioProdutoInexistenteException(long ProdutoId)
    : Exception($"CARDAPIO_PRODUTO_INEXISTENTE: produto {ProdutoId} não encontrado ou inativo em sigov.educacao_merenda_produto para o tenant atual; cadastre o produto antes de montar o cardápio.");

/// <summary>Produto repetido na criação de cardápio (HTTP 409, código CARDAPIO_PRODUTO_DUPLICADO).</summary>
public sealed class CardapioProdutoDuplicadoException(long ProdutoId)
    : Exception($"CARDAPIO_PRODUTO_DUPLICADO: produto {ProdutoId} informado mais de uma vez no cardápio; cada produto deve aparecer uma única vez.");

/// <summary>Cardápio vigente já existe para a escola e data de início (HTTP 409, código CARDAPIO_PERIODO_EXISTENTE).</summary>
public sealed class CardapioPeriodoExistenteException(long EscolaId, DateOnly DataInicio)
    : Exception($"CARDAPIO_PERIODO_EXISTENTE: já existe cardápio vigente em sigov.educacao_merenda_cardapio para a escola {EscolaId} com início em {DataInicio:yyyy-MM-dd}; encerre ou cancele o atual antes de criar outro para o mesmo período.");

// Sprint 6 (merenda operação): distribuição de refeição com consumo real de estoque.
// Falhas nomeadas explícitas (regra 13); nada municipal hardcoded (regra 24).

/// <summary>Produto inexistente/inativo para o tenant atual no registro de estoque (HTTP 404, código MERENDA_PRODUTO_INEXISTENTE).</summary>
public sealed class MerendaProdutoInexistenteException(long ProdutoId)
    : Exception($"MERENDA_PRODUTO_INEXISTENTE: produto {ProdutoId} não encontrado ou inativo em sigov.educacao_merenda_produto para o tenant atual; cadastre o produto antes de operar o estoque.");

/// <summary>Cardápio inexistente/inativo para o tenant na distribuição de refeição (HTTP 404, código DISTRIBUICAO_CARDAPIO_INEXISTENTE).</summary>
public sealed class DistribuicaoCardapioInexistenteException(long CardapioId)
    : Exception($"DISTRIBUICAO_CARDAPIO_INEXISTENTE: cardápio {CardapioId} não encontrado ou inativo em sigov.educacao_merenda_cardapio para o tenant atual.");

/// <summary>Cardápio de outra entidade do contexto institucional (HTTP 409, código CARDAPIO_FORA_DO_ESCOPO).</summary>
public sealed class CardapioForaDoEscopoException(long CardapioId, long CardapioEntidadeId, long ContextoEntidadeId)
    : Exception($"CARDAPIO_FORA_DO_ESCOPO: cardápio {CardapioId} pertence à entidade {CardapioEntidadeId} e o comando está no contexto da entidade {ContextoEntidadeId}; troque de contexto institucional antes de distribuir.");

/// <summary>Cardápio ainda não aprovado/ativo na distribuição (HTTP 409, código CARDAPIO_NAO_APROVADO).</summary>
public sealed class CardapioNaoAprovadoException(long CardapioId, string StatusAtual)
    : Exception($"CARDAPIO_NAO_APROVADO: o cardápio {CardapioId} está com status {StatusAtual}; distribuições exigem cardápio ATIVO (aprove o cardápio antes de distribuir refeições).");

/// <summary>Data de atendimento fora do período do cardápio (HTTP 409, código DATA_FORA_DO_PERIODO).</summary>
public sealed class DataForaDoPeriodoException(long CardapioId, DateOnly Data, DateOnly Inicio, DateOnly Fim)
    : Exception($"DATA_FORA_DO_PERIODO: data de atendimento {Data:yyyy-MM-dd} fora do período do cardápio {CardapioId} ({Inicio:yyyy-MM-dd} a {Fim:yyyy-MM-dd}).");

/// <summary>Produto do item não consta no menu do cardápio (HTTP 400, código PRODUTO_FORA_DO_MENU).</summary>
public sealed class ProdutoForaDoMenuException(long ProdutoId, long CardapioId)
    : Exception($"PRODUTO_FORA_DO_MENU: produto {ProdutoId} não consta nos itens do cardápio {CardapioId}; distribua apenas produtos do menu publicado.");

/// <summary>Distribuição vigente já existe para o cardápio e data (HTTP 409, código DISTRIBUICAO_DIA_EXISTENTE).</summary>
public sealed class DistribuicaoDiaExistenteException(long CardapioId, DateOnly Data)
    : Exception($"DISTRIBUICAO_DIA_EXISTENTE: já existe distribuição vigente em sigov.educacao_merenda_distribuicao para o cardápio {CardapioId} na data {Data:yyyy-MM-dd}; cancele-a antes de registrar outra para o mesmo dia.");

/// <summary>Saldo de estoque insuficiente para a quantidade distribuída (HTTP 409, código ESTOQUE_INSUFICIENTE).</summary>
public sealed class EstoqueInsuficienteException(long ProdutoId, decimal Disponivel, decimal Solicitado)
    : Exception($"ESTOQUE_INSUFICIENTE: saldo disponível {Disponivel:0.####} é menor que a quantidade {Solicitado:0.####} solicitada para o produto {ProdutoId}; faça um lançamento de entrada antes de distribuir.");

// Sprint 7 (biblioteca circulação): emprestimo, devolução e reserva com
// unicidade de vigência por exemplar. Falhas nomeadas explícitas (regra 13);
// nada municipal hardcoded (regra 24).

/// <summary>Exemplar inexistente/inativo para o tenant atual na circulação (HTTP 404, código BIBLIOTECA_EXEMPLAR_INEXISTENTE).</summary>
public sealed class BibliotecaExemplarInexistenteException(long ExemplarId)
    : Exception($"BIBLIOTECA_EXEMPLAR_INEXISTENTE: exemplar {ExemplarId} não encontrado ou inativo em sigov.educacao_biblioteca_exemplar para o tenant atual; cadastre o exemplar antes de operar a circulação.");

/// <summary>Emprestimo inexistente/inativo para o tenant atual na devolução (HTTP 404, código BIBLIOTECA_EMPRESTIMO_INEXISTENTE).</summary>
public sealed class BibliotecaEmprestimoInexistenteException(long EmprestimoId)
    : Exception($"BIBLIOTECA_EMPRESTIMO_INEXISTENTE: empréstimo {EmprestimoId} não encontrado em sigov.educacao_biblioteca_emprestimo para o tenant atual.");

/// <summary>Aluno tomador inexistente/inativo para o tenant atual no empréstimo (HTTP 404, código BIBLIOTECA_ALUNO_INEXISTENTE).</summary>
public sealed class BibliotecaAlunoInexistenteException(long AlunoId)
    : Exception($"BIBLIOTECA_ALUNO_INEXISTENTE: aluno {AlunoId} não encontrado ou inativo em sigov.aluno para o tenant atual; cadastre/ative o aluno antes do empréstimo.");

/// <summary>Tomador do empréstimo ausente ou ambíguo (HTTP 400, código EMPRESTIMO_TOMADOR_INVALIDO).</summary>
public sealed class EmprestimoTomadorInvalidoException(long? AlunoId, long? ServidorId)
    : Exception($"EMPRESTIMO_TOMADOR_INVALIDO: informe exatamente um tomador para o empréstimo (alunoId={AlunoId?.ToString() ?? "nulo"}; servidorId={ServidorId?.ToString() ?? "nulo"}).");

/// <summary>Servidor tomador inexistente/inativo para o tenant atual no empréstimo (HTTP 404, código BIBLIOTECA_SERVIDOR_INEXISTENTE).</summary>
public sealed class BibliotecaServidorInexistenteException(long ServidorId)
    : Exception($"BIBLIOTECA_SERVIDOR_INEXISTENTE: servidor {ServidorId} não encontrado ou inativo em sigov.servidor para o tenant atual; cadastre/ative o servidor antes do empréstimo.");

/// <summary>Prazo de devolução anterior à data de lançamento (HTTP 400, código PRAZO_EMPRESTIMO_INVALIDO).</summary>
public sealed class PrazoEmprestimoInvalidoException(DateOnly Prazo, DateOnly DataLancamento)
    : Exception($"PRAZO_EMPRESTIMO_INVALIDO: prazo de devolução {Prazo:yyyy-MM-dd} anterior à data de lançamento {DataLancamento:yyyy-MM-dd}; informe um prazo a partir de hoje.");

/// <summary>Exemplar de outra entidade do contexto institucional (HTTP 409, código EXEMPLAR_FORA_DO_ESCOPO).</summary>
public sealed class ExemplarForaDoEscopoException(long ExemplarId, long ExemplarEntidadeId, long ContextoEntidadeId)
    : Exception($"EXEMPLAR_FORA_DO_ESCOPO: exemplar {ExemplarId} pertence à entidade {ExemplarEntidadeId} e o comando está no contexto da entidade {ContextoEntidadeId}; troque de contexto institucional antes de operar a circulação.");

/// <summary>Exemplar fora do status circulável na operação (HTTP 409, código EXEMPLAR_NAO_DISPONIVEL).</summary>
public sealed class ExemplarNaoDisponivelException(long ExemplarId, string StatusAtual)
    : Exception($"EXEMPLAR_NAO_DISPONIVEL: o exemplar {ExemplarId} está com status {StatusAtual}; a circulação exige exemplar ATIVO/DISPONÍVEL (ajuste o status antes de operar).");

/// <summary>Já existe empréstimo vigente para o exemplar (HTTP 409, código EXEMPLAR_INDISPONIVEL; backstop ux_biblioteca_emprestimo_tenant_exemplar_vigencia).</summary>
public sealed class ExemplarIndisponivelException(long ExemplarId)
    : Exception($"EXEMPLAR_INDISPONIVEL: já existe empréstimo vigente em sigov.educacao_biblioteca_emprestimo para o exemplar {ExemplarId}; registre a devolução antes de emprestar novamente.");

/// <summary>Emprestimo já encerrado/devolvido na devolução (HTTP 409, código EMPRESTIMO_JA_DEVOLVIDO).</summary>
public sealed class EmprestimoJaDevolvidoException(long EmprestimoId, string StatusAtual)
    : Exception($"EMPRESTIMO_JA_DEVOLVIDO: o empréstimo {EmprestimoId} está com status {StatusAtual}; apenas empréstimos ATIVOS podem ser devolvidos.");

/// <summary>Já existe reserva vigente para o exemplar - fila de tamanho 1 (HTTP 409, código RESERVA_VIGENTE_EXISTENTE; backstop ux_biblioteca_reserva_tenant_exemplar_vigencia).</summary>
public sealed class ReservaVigenteExisteException(long ExemplarId)
    : Exception($"RESERVA_VIGENTE_EXISTENTE: já existe reserva vigente em sigov.educacao_biblioteca_reserva para o exemplar {ExemplarId} (fila de tamanho 1); cancele-a antes de registrar nova reserva.");

// Sprint 3 (biblioteca circulação completa, BR-3.1 a BR-3.8): limite por tomador, devolução
// idempotente com data efetiva, renovação limitada a partir do prazo original, ocorrência com
// compensação única, desbloqueio justificável e inventário físico com divergência justificada.

/// <summary>Exemplar bloqueado por ocorrência na operação (HTTP 409, código EXEMPLAR_BLOQUEADO; BR-3.4).</summary>
public sealed class ExemplarBloqueadoException(long ExemplarId)
    : Exception($"EXEMPLAR_BLOQUEADO: o exemplar {ExemplarId} está BLOQUEADO por ocorrência vigente em sigov.educacao_biblioteca_ocorrencia; registre o desbloqueio com justificativa (POST /api/educacao/biblioteca/exemplares/{ExemplarId}/desbloquear) antes de emprestar ou reservar.");

/// <summary>Limite de empréstimos vigentes do tomador atingido (HTTP 409, código TOMADOR_EMPRESTIMOS_MAX_ATINGIDO; BR-3.1).</summary>
public sealed class EmprestimoTomadorLimiteExcedidoException(long? AlunoId, long? ServidorId, int Maximo)
    : Exception($"TOMADOR_EMPRESTIMOS_MAX_ATINGIDO: o tomador (alunoId={AlunoId?.ToString() ?? "nulo"}; servidorId={ServidorId?.ToString() ?? "nulo"}) já tem {Maximo} empréstimos vigentes, o máximo permitido pelo parâmetro do tenant EMPRESTIMO_MAX_VIGENTES_TOMADOR; registre uma devolução antes de emprestar novamente.");

/// <summary>Data efetiva de devolução posterior à data operacional (HTTP 400, código DATA_DEVOLUCAO_INVALIDA; BR-3.2).</summary>
public sealed class DataDevolucaoInvalidaException(DateOnly DataDevolucao, DateOnly DataOperacional)
    : Exception($"DATA_DEVOLUCAO_INVALIDA: a data efetiva de devolução {DataDevolucao:yyyy-MM-dd} é posterior à data operacional {DataOperacional:yyyy-MM-dd}; informe a data em que a devolução realmente ocorreu.");

/// <summary>Emprestimo encerrado por ocorrência na devolução (HTTP 409, código EMPRESTIMO_ENCERRADO_POR_OCORRENCIA; BR-3.4).</summary>
public sealed class EmprestimoEncerradoPorOcorrenciaException(long EmprestimoId, long OcorrenciaId)
    : Exception($"EMPRESTIMO_ENCERRADO_POR_OCORRENCIA: o empréstimo {EmprestimoId} foi encerrado pela ocorrência {OcorrenciaId}; se a ocorrência foi equivocada, estorne-a antes (POST /api/educacao/biblioteca/ocorrencias/{OcorrenciaId}/estornar).");

/// <summary>Renovação sobre empréstimo não vigente (HTTP 409, código RENOVAR_EMPRESTIMO_NAO_VIGENTE; BR-3.3).</summary>
public sealed class RenovacaoEmprestimoNaoVigenteException(long EmprestimoId, string StatusAtual)
    : Exception($"RENOVAR_EMPRESTIMO_NAO_VIGENTE: o empréstimo {EmprestimoId} está com status {StatusAtual}; apenas empréstimos ATIVOS podem ser renovados.");

/// <summary>Renovação com reserva pendente no exemplar (HTTP 409, código RENOVAR_RESERVA_VIGENTE; BR-3.3).</summary>
public sealed class RenovacaoReservaVigenteException(long ExemplarId)
    : Exception($"RENOVAR_RESERVA_VIGENTE: o exemplar {ExemplarId} possui reserva pendente; a renovação só ocorre sem reserva no exemplar (cancele a reserva ou aguarde a devolução).");

/// <summary>Limite de renovações do empréstimo esgotado (HTTP 409, código RENOVAR_LIMITE_EXCEDIDO; BR-3.3).</summary>
public sealed class RenovacaoLimiteExcedidoException(long EmprestimoId, int Renovacoes, int Maximo)
    : Exception($"RENOVAR_LIMITE_EXCEDIDO: o empréstimo {EmprestimoId} já tem {Renovacoes} renovações, o máximo permitido pelo parâmetro do tenant RENOVACAO_MAX_POR_EMPRESTIMO ({Maximo}).");

/// <summary>Tipo de ocorrência fora do conjunto canônico (HTTP 400, código OCORRENCIA_TIPO_INVALIDO; BR-3.4).</summary>
public sealed class OcorrenciaTipoInvalidoException(string Tipo)
    : Exception($"OCORRENCIA_TIPO_INVALIDO: tipo '{Tipo}' inválido; os tipos canônicos de ocorrência de biblioteca são PERDA, DANO, EXTRAVIO e BLOQUEIO.");

/// <summary>Ocorrência sem motivo (HTTP 400, código OCORRENCIA_MOTIVO_OBRIGATORIO; BR-3.4).</summary>
public sealed class OcorrenciaMotivoObrigatorioException()
    : Exception("OCORRENCIA_MOTIVO_OBRIGATORIO: toda ocorrência exige motivo descrevendo o fato registrado (perda, dano, extravio ou bloqueio).");

/// <summary>Ocorrência inexistente/inativa para o tenant atual (HTTP 404, código BIBLIOTECA_OCORRENCIA_INEXISTENTE).</summary>
public sealed class BibliotecaOcorrenciaInexistenteException(long OcorrenciaId)
    : Exception($"BIBLIOTECA_OCORRENCIA_INEXISTENTE: ocorrência {OcorrenciaId} não encontrada em sigov.educacao_biblioteca_ocorrencia para o tenant atual.");

/// <summary>Estorno de ocorrência sem motivo (HTTP 400, código ESTORNO_OCORRENCIA_MOTIVO_OBRIGATORIO; BR-3.4).</summary>
public sealed class OcorrenciaEstornoMotivoObrigatorioException()
    : Exception("ESTORNO_OCORRENCIA_MOTIVO_OBRIGATORIO: todo estorno de ocorrência exige justificativa descrevendo o motivo da reversão.");

/// <summary>Inventário inexistente/inativo para o tenant atual (HTTP 404, código BIBLIOTECA_INVENTARIO_INEXISTENTE; BR-3.5).</summary>
public sealed class BibliotecaInventarioInexistenteException(long InventarioId)
    : Exception($"BIBLIOTECA_INVENTARIO_INEXISTENTE: inventário {InventarioId} não encontrado em sigov.educacao_biblioteca_inventario para o tenant atual.");

/// <summary>Item de inventário inexistente no snapshot (HTTP 404, código BIBLIOTECA_INVENTARIO_ITEM_INEXISTENTE; BR-3.5).</summary>
public sealed class BibliotecaInventarioItemInexistenteException(long InventarioId, long ExemplarId)
    : Exception($"BIBLIOTECA_INVENTARIO_ITEM_INEXISTENTE: o exemplar {ExemplarId} não possui item no inventário {InventarioId}; lance a contagem primeiro (POST /api/educacao/biblioteca/inventarios/{InventarioId}/itens/contar).");

/// <summary>Inventário já fechado na operação (HTTP 409, código INVENTARIO_JA_FECHADO; BR-3.5).</summary>
public sealed class InventarioJaFechadoException(long InventarioId, string StatusAtual)
    : Exception($"INVENTARIO_JA_FECHADO: o inventário {InventarioId} está com status {StatusAtual}; apenas inventários ABERTOS aceitam contagem, justificativa ou fechamento.");

/// <summary>Inventário com divergências ainda abertas no fechamento (HTTP 409, código INVENTARIO_DIVERGENCIAS_EM_ABERTO; BR-3.5).</summary>
public sealed class InventarioDivergenciasAbertasException(long InventarioId, long Abertas)
    : Exception($"INVENTARIO_DIVERGENCIAS_EM_ABERTO: o inventário {InventarioId} tem {Abertas} divergência(s) sem desfecho; justifique cada uma (AJUSTE ou DISPENSA com justificativa) antes de fechar.");

/// <summary>Desfecho de divergência fora do conjunto canônico (HTTP 400, código INVENTARIO_DESFECHO_INVALIDO; BR-3.5).</summary>
public sealed class InventarioDesfechoInvalidoException(string Desfecho)
    : Exception($"INVENTARIO_DESFECHO_INVALIDO: desfecho '{Desfecho}' inválido; os desfechos canônicos da divergência são AJUSTE e DISPENSA.");

/// <summary>Justificativa de divergência ausente (HTTP 400, código INVENTARIO_JUSTIFICATIVA_OBRIGATORIA; BR-3.5).</summary>
public sealed class InventarioJustificativaObrigatorioException()
    : Exception("INVENTARIO_JUSTIFICATIVA_OBRIGATORIA: AJUSTE e DISPENSA de divergência exigem justificativa descrevendo a decisão.");

/// <summary>Justificativa de item sem divergência ou já justificado (HTTP 409, código INVENTARIO_ITEM_SEM_DIVERGENCIA; BR-3.5).</summary>
public sealed class InventarioItemSemDivergenciaException(long InventarioId, long ExemplarId)
    : Exception($"INVENTARIO_ITEM_SEM_DIVERGENCIA: o item do exemplar {ExemplarId} no inventário {InventarioId} não apresenta divergência pendente (contagem ausente, contagem igual à posição do sistema ou desfecho já registrado).");

/// <summary>Exemplar não bloqueado no desbloqueio (HTTP 409, código EXEMPLAR_NAO_BLOQUEADO; BR-3.4).</summary>
public sealed class ExemplarNaoBloqueadoException(long ExemplarId, string StatusAtual)
    : Exception($"EXEMPLAR_NAO_BLOQUEADO: o exemplar {ExemplarId} está com status {StatusAtual}; o desbloqueio só se aplica a exemplares BLOQUEADOS.");

/// <summary>Desbloqueio sem justificativa (HTTP 400, código DESBLOQUEIO_JUSTIFICATIVA_OBRIGATORIA; BR-3.4).</summary>
public sealed class DesbloqueioJustificativaObrigatorioException()
    : Exception("DESBLOQUEIO_JUSTIFICATIVA_OBRIGATORIA: o desbloqueio de exemplar exige justificativa descrevendo o motivo da liberação.");

/// <summary>Quantidade de contagem física inválida (HTTP 400, código INVENTARIO_QUANTIDADE_INVALIDA; BR-3.5).</summary>
public sealed class InventarioQuantidadeInvalidaException(int Quantidade)
    : Exception($"INVENTARIO_QUANTIDADE_INVALIDA: quantidade {Quantidade} inválida; a contagem física é um inteiro não negativo (0 = exemplar ausente na contagem; 1 = exemplar presente).");

/// <summary>Rota de transporte inexistente/inativa no tenant (HTTP 404, código TRANSPORTE_ROTA_INEXISTENTE).</summary>
public sealed class TransporteRotaInexistenteException(long RotaId)
    : Exception($"TRANSPORTE_ROTA_INEXISTENTE: a rota {RotaId} não existe em sigov.educacao_transporte_rota para este tenant (ou foi inativada/excluída).");

/// <summary>Rota não está em status ativo para operação (HTTP 409, código TRANSPORTE_ROTA_NAO_VIGENTE).</summary>
public sealed class TransporteRotaNaoVigenteException(long RotaId, string StatusAtual)
    : Exception($"TRANSPORTE_ROTA_NAO_VIGENTE: a rota {RotaId} está com status {StatusAtual}; pontos e vínculos de aluno exigem rota ATIVA.");

/// <summary>Comando de rota sem escola nem região, ou com ambas (HTTP 400, código ROTA_ESCOLA_OU_REGIAO_INVALIDA).</summary>
public sealed class TransporteRotaEscolaOuRegiaoInvalidoException()
    : Exception("ROTA_ESCOLA_OU_REGIAO_INVALIDA: o lançamento de rota exige exatamente uma de escolaId ou região — para atendimento multi-esfera (regra 24), rotas de órgão/região usam regiao e rotas de unidade escolar usam escolaId.");

/// <summary>Ordem de ponto fora do intervalo válido (HTTP 400, código PONTO_ORDEM_INVALIDA).</summary>
public sealed class TransportePontoOrdemInvalidaException(int Ordem)
    : Exception($"PONTO_ORDEM_INVALIDA: a ordem {Ordem} é inválida; ordens de ponto devem ser inteiros positivos, começando em 1.");

/// <summary>Ordem de ponto já ocupada na rota (HTTP 409, código PONTO_ORDENACAO_EXISTENTE; backstop ux_transporte_ponto_tenant_rota_ordem).</summary>
public sealed class TransportePontoOrdemExistenteException(long RotaId, int Ordem)
    : Exception($"PONTO_ORDENACAO_EXISTENTE: já existe ponto vigente na ordem {Ordem} da rota {RotaId}; use outra ordem ou cancele o ponto existente.");

/// <summary>Ponto de rota inexistente/inativo no tenant (HTTP 404, código TRANSPORTE_PONTO_INEXISTENTE).</summary>
public sealed class TransportePontoInexistenteException(long PontoId)
    : Exception($"TRANSPORTE_PONTO_INEXISTENTE: o ponto {PontoId} não existe em sigov.educacao_transporte_rota_ponto para este tenant (ou foi inativado/excluído).");

/// <summary>Ponto informado pertence a outra rota (HTTP 409, código PONTO_ROTA_DIVERGENTE).</summary>
public sealed class TransportePontoRotaDivergenteException(long PontoId, long RotaDoPonto, long RotaInformada)
    : Exception($"PONTO_ROTA_DIVERGENTE: o ponto {PontoId} pertence à rota {RotaDoPonto}; o vínculo foi informado para a rota {RotaInformada}.");

/// <summary>Aluno inexistente/inativo no tenant para vínculo de transporte (HTTP 404, código TRANSPORTE_ALUNO_INEXISTENTE).</summary>
public sealed class TransporteAlunoInexistenteException(long AlunoId)
    : Exception($"TRANSPORTE_ALUNO_INEXISTENTE: o aluno {AlunoId} não existe em sigov.aluno para este tenant (ou está inativo); cadastre/ative o aluno antes de vinculá-lo à rota.");

/// <summary>Turno do vínculo divergente do turno da rota (HTTP 400, código TURNO_VINCULO_DIVERGENTE).</summary>
public sealed class TransporteTurnoVinculoDivergenteException(string TurnoVinculo, string TurnoRota)
    : Exception($"TURNO_VINCULO_DIVERGENTE: o turno do vínculo ({TurnoVinculo}) difere do turno da rota ({TurnoRota}); omita o turno no vínculo para herdá-lo da rota.");

/// <summary>Aluno já possui vínculo vigente com a rota no mesmo turno (HTTP 409, código ALUNO_JA_VINCULADO_A_ROTA; backstop ux_educacao_transporte_aluno_rota_turno).</summary>
public sealed class TransporteAlunoJaVinculadoARotaException(long AlunoId, long RotaId, string Turno)
    : Exception($"ALUNO_JA_VINCULADO_A_ROTA: o aluno {AlunoId} já possui vínculo vigente com a rota {RotaId} no turno {Turno}; cancele o vínculo antes de recriá-lo.");

/// <summary>Parâmetro FDEB vigente já existente com o mesmo código no tenant (HTTP 409, código FUNDEB_PARAMETRO_CODIGO_EXISTENTE; backstop ux_fundeb_parametro_tenant_codigo).</summary>
public sealed class FundebParametroCodigoExistenteException(string Codigo)
    : Exception($"FUNDEB_PARAMETRO_CODIGO_EXISTENTE: já existe parâmetro FDEB vigente com o código {Codigo} neste tenant; cancele o parâmetro existente antes de lançá-lo novamente.");

/// <summary>Receita FDEB já lançada para a competência/fonte no tenant (HTTP 409, código FUNDEB_RECEITA_DUPLICADA; backstop ux_fundeb_receita_tenant_competencia_fonte).</summary>
public sealed class FundebReceitaDuplicadaException(string Competencia, string Fonte)
    : Exception($"FUNDEB_RECEITA_DUPLICADA: já existe receita FDEB vigente na competência {Competencia} com a fonte {Fonte}; use outra competência/fonte ou cancele a receita existente.");

/// <summary>Centro de custo inexistente/inativo no tenant (HTTP 404, código CUSTO_CENTRO_INEXISTENTE).</summary>
public sealed class CustoCentroInexistenteException(long CentroCustoId)
    : Exception($"CUSTO_CENTRO_INEXISTENTE: o centro de custo {CentroCustoId} não existe em sigov.educacao_custo_centro para este tenant (ou está inativo); cadastre/ative o centro antes de lançar a aplicação.");

/// <summary>Centro de custo não vigente para aplicação (HTTP 409, código CUSTO_CENTRO_NAO_VIGENTE).</summary>
public sealed class CustoCentroNaoVigenteException(long CentroCustoId, string Status)
    : Exception($"CUSTO_CENTRO_NAO_VIGENTE: o centro de custo {CentroCustoId} está com status {Status}; apenas centros ATIVOS aceitam aplicação.");

/// <summary>Código de centro de custo já ocupado por centro vigente no tenant (HTTP 409, código CUSTO_CENTRO_CODIGO_EXISTENTE; backstop ux_custo_centro_tenant_codigo).</summary>
public sealed class CustoCentroCodigoExistenteException(string Codigo)
    : Exception($"CUSTO_CENTRO_CODIGO_EXISTENTE: já existe centro de custo vigente com o código {Codigo} neste tenant; use outro código ou cancele o centro existente.");

/// <summary>Indicador vigente já existe com o mesmo código no tenant (HTTP 409, código INDICADOR_CODIGO_EXISTENTE; backstop ux_indicador_tenant_codigo).</summary>
public sealed class IndicadorCodigoExistenteException(string Codigo)
    : Exception($"INDICADOR_CODIGO_EXISTENTE: já existe indicador vigente com o código {Codigo} neste tenant; use outro código ou cancele o indicador existente.");

/// <summary>Indicador inexistente/inativo no tenant (HTTP 404, código INDICADOR_INEXISTENTE).</summary>
public sealed class IndicadorInexistenteException(long IndicadorId)
    : Exception($"INDICADOR_INEXISTENTE: o indicador {IndicadorId} não existe em sigov.educacao_indicador para este tenant (ou está inativo); cadastre/ative o indicador antes de lançar valores.");

/// <summary>Indicador não vigente para lançamento de valor (HTTP 409, código INDICADOR_NAO_VIGENTE).</summary>
public sealed class IndicadorNaoVigenteException(long IndicadorId, string Status)
    : Exception($"INDICADOR_NAO_VIGENTE: o indicador {IndicadorId} está com status {Status}; apenas indicadores ATIVOS aceitam lançamento de valor.");

/// <summary>Valor já lançado para o indicador/ano no tenant (HTTP 409, código VALOR_INDICADOR_EXISTENTE; backstop ux_indicador_valor_tenant_ref_ano).</summary>
public sealed class ValorIndicadorExistenteException(long IndicadorId, int Ano)
    : Exception($"VALOR_INDICADOR_EXISTENTE: já existe valor vigente do indicador {IndicadorId} para o ano {Ano} neste tenant; use outro ano ou cancele o valor existente.");

/// <summary>Projeção de matrícula já lançada para a escola/ano no tenant (HTTP 409, código PROJECAO_MATRICULA_EXISTENTE; backstop ux_projecao_matricula_tenant_escola_ano).</summary>
public sealed class ProjecaoMatriculaExistenteException(long EscolaId, int Ano)
    : Exception($"PROJECAO_MATRICULA_EXISTENTE: já existe projeção vigente da escola {EscolaId} para o ano {Ano} neste tenant; use outro ano ou cancele a projeção existente.");

/// <summary>Lote Educacenso vigente já existe para o ano no tenant (HTTP 409, código LOTE_EDUCACENSO_ANO_EXISTENTE; backstop ux_educacenso_lote_tenant_ano).</summary>
public sealed class LoteEducacensoAnoExistenteException(int Ano)
    : Exception($"LOTE_EDUCACENSO_ANO_EXISTENTE: já existe lote Educacenso vigente para o ano {Ano} neste tenant; use outro ano ou cancele o lote existente.");

/// <summary>Lote Educacenso inexistente/inativo no tenant (HTTP 404, código LOTE_EDUCACENSO_INEXISTENTE).</summary>
public sealed class LoteEducacensoInexistenteException(long LoteId)
    : Exception($"LOTE_EDUCACENSO_INEXISTENTE: o lote Educacenso {LoteId} não existe em sigov.educacao_educacenso_lote para este tenant (ou está inativo); lance o lote antes de registrar inconsistências.");

/// <summary>Lote Educacenso não vigente para registro de inconsistência (HTTP 409, código LOTE_EDUCACENSO_NAO_VIGENTE).</summary>
public sealed class LoteEducacensoNaoVigenteException(long LoteId, string Status)
    : Exception($"LOTE_EDUCACENSO_NAO_VIGENTE: o lote Educacenso {LoteId} está com status {Status}; apenas lotes ATIVOS aceitam registro de inconsistência.");

// Ciclo 2 Sprint 1 (merenda integridade transacional): comandos tipados de estoque/estorno e
// revalidação de distribuição na mesma transação. Falhas nomeadas explícitas (regra 13);
// nada municipal hardcoded (regra 24).

/// <summary>Operação de lançamento de estoque fora do domínio ABERTURA/ENTRADA/AJUSTE (HTTP 400, código ESTOQUE_OPERACAO_INVALIDA).</summary>
public sealed class EstoqueOperacaoInvalidaException(string Operacao)
    : Exception($"ESTOQUE_OPERACAO_INVALIDA: a operação '{Operacao}' é inválida; use ABERTURA, ENTRADA ou AJUSTE.");

/// <summary>Produto sem linha de estoque aberta para ENTRADA/AJUSTE/ESTORNO (HTTP 404, código ESTOQUE_NAO_ABERTO).</summary>
public sealed class EstoqueNaoAbertoException(long ProdutoId)
    : Exception($"ESTOQUE_NAO_ABERTO: o produto {ProdutoId} não possui linha de estoque aberta em sigov.educacao_merenda_estoque para este tenant; registre uma ABERTURA antes de operar entradas, ajustes ou estornos.");

/// <summary>Já existe linha de estoque vigente para o produto no lançamento ABERTURA (HTTP 409, código ESTOQUE_ABERTURA_EXISTENTE; backstop ux_merenda_estoque_tenant_produto).</summary>
public sealed class EstoqueAberturaExisteException(long ProdutoId)
    : Exception($"ESTOQUE_ABERTURA_EXISTENTE: o produto {ProdutoId} já possui linha de estoque vigente; use ENTRADA ou AJUSTE para alterar o saldo.");

/// <summary>Saldo resultante negativo em ajuste/estorno (HTTP 409, código SALDO_NEGATIVO_REJEITADO).</summary>
public sealed class SaldoNegativoRejeitadoException(long ProdutoId, decimal NovoSaldo)
    : Exception($"SALDO_NEGATIVO_REJEITADO: a operação deixaria o saldo do produto {ProdutoId} em {NovoSaldo:0.####}; ajuste o lançamento ou registre entrada antes.");

/// <summary>Chave de idempotência já usada para conteúdo diferente (HTTP 409, código CHAVE_IDEMPOTENCIA_CONFLITO; backstop ux_merenda_movimento_tenant_chave).</summary>
public sealed class ChaveIdempotenciaConflitoException(string Chave)
    : Exception($"CHAVE_IDEMPOTENCIA_CONFLITO: a chave de idempotência '{Chave}' já foi usada para outra operação; reenvie o mesmo conteúdo ou use uma nova chave.");

/// <summary>Estorno sem justificativa (HTTP 400, código ESTORNO_MOTIVO_OBRIGATORIO).</summary>
public sealed class EstornoMotivoObrigatorioException()
    : Exception("ESTORNO_MOTIVO_OBRIGATORIO: todo estorno exige justificativa descrevendo o motivo da compensação.");

/// <summary>Movimento base inexistente/inativo para estorno (HTTP 404, código ESTORNO_BASE_INEXISTENTE).</summary>
public sealed class EstornoBaseInexistenteException(long MovimentoId)
    : Exception($"ESTORNO_BASE_INEXISTENTE: o movimento {MovimentoId} não existe em sigov.educacao_merenda_movimento para este tenant.");

/// <summary>Tipo de movimento base não estornável (HTTP 409, código ESTORNO_BASE_INVALIDA).</summary>
public sealed class EstornoBaseInvalidaException(long MovimentoId, string Tipo)
    : Exception($"ESTORNO_BASE_INVALIDA: o movimento {MovimentoId} tem tipo '{Tipo}'; somente movimentos ENTRADA, SAIDA e AJUSTE podem ser estornados (estorno de estorno é proibido).");

/// <summary>Distribuição com consumo de estoque bloqueada contra cancelamento genérico (HTTP 409, código DISTRIBUICAO_ESTORNO_OBRIGATORIO).</summary>
public sealed class DistribuicaoEstornoObrigatorioException(long DistribuicaoId)
    : Exception($"DISTRIBUICAO_ESTORNO_OBRIGATORIO: a distribuição {DistribuicaoId} já consumiu estoque; cancele-a pelo comando tipado de estorno (POST /api/educacao/merenda/distribuicoes/{DistribuicaoId}/estornar) para registrar a compensação do saldo.");

/// <summary>Distribuição sem condição de estorno (estado final sem efeitos nem compensação) (HTTP 409, código DISTRIBUICAO_ESTORNO_INDISPONIVEL).</summary>
public sealed class DistribuicaoEstornoIndisponivelException(long DistribuicaoId, string StatusAtual)
    : Exception($"DISTRIBUICAO_ESTORNO_INDISPONIVEL: a distribuição {DistribuicaoId} está com status {StatusAtual} sem consumo registrado para compensar; apenas distribuições ativas ou com efeitos pendentes podem ser estornadas.");

// Ciclo 2 Sprint 4 (transporte escolar: da rota à viagem, BR-4.1 a BR-4.8): veiculo apto por
// situacao documental/inspecao, motorista por habilitacao+categoria compativel, viagem sem
// sobreposicao (backstops parciais por veiculo x data e motorista x data), presenca com
// fechamento e estorno unitario, ocorrencia com gravidade/desfecho e carona de horas, custo
// realizado sem edicao manual. Falhas nomeadas explicitas (regra 13); nada municipal hardcoded (regra 24).

/// <summary>Veículo inexistente/inativo para o tenant atual (HTTP 404, código VEICULO_INEXISTENTE; BR-4.1).</summary>
public sealed class TransporteVeiculoInexistenteException(long VeiculoId)
    : Exception($"VEICULO_INEXISTENTE: o veículo {VeiculoId} não existe em sigov.educacao_transporte_veiculo para este tenant (ou está inativo); cadastre/ative o veículo antes de alocá-lo em viagem.");

/// <summary>Placa já cadastrada como veículo vigente no tenant (HTTP 409, código VEICULO_PLACA_EXISTENTE; backstop ux_transporte_veiculo_tenant_placa; BR-4.1).</summary>
public sealed class TransporteVeiculoPlacaExistenteException(string Placa)
    : Exception($"VEICULO_PLACA_EXISTENTE: já existe veículo vigente com a placa {Placa} neste tenant; use outra placa ou ative o veículo existente.");

/// <summary>Situação documental fora do conjunto canônico no cadastro (HTTP 400, código VEICULO_SITUACAO_INVALIDA; BR-4.1).</summary>
public sealed class TransporteVeiculoSituacaoInvalidaException(string Situacao)
    : Exception($"VEICULO_SITUACAO_INVALIDA: situação documental '{Situacao}' inválida; as situações canônicas são REGULAR, VENCIDA e BAIXA.");

/// <summary>Capacidade inválida no cadastro do veículo (HTTP 400, código VEICULO_CAPACIDADE_INVALIDA; BR-4.1).</summary>
public sealed class TransporteVeiculoCapacidadeInvalidaException(int Capacidade)
    : Exception($"VEICULO_CAPACIDADE_INVALIDA: capacidade {Capacidade} inválida; a capacidade de assentos é um inteiro positivo.");

/// <summary>Veículo em situação documental vencida/baixa impede alocação em viagem (HTTP 409, código VEICULO_INAPTO; BR-4.1).</summary>
public sealed class TransporteVeiculoInaptoException(long VeiculoId, string SituacaoDocumental)
    : Exception($"VEICULO_INAPTO: o veículo {VeiculoId} está com situação documental {SituacaoDocumental}; apenas veículos REGULARES podem ser alocados em viagem (regularize a documentação antes).");

/// <summary>Inspeção/revisão do veículo vencida e modo BLOQUEIO ativo (HTTP 409, código VEICULO_INSPECCAO_VENCIDA; BR-4.1, parâmetro TRANSPORTE_INSPECCAO_MODO).</summary>
public sealed class TransporteVeiculoInspecaoVencidaException(long VeiculoId, DateOnly Validade)
    : Exception($"VEICULO_INSPECCAO_VENCIDA: a inspeção/revisão do veículo {VeiculoId} venceu em {Validade:yyyy-MM-dd} e o parâmetro do tenant TRANSPORTE_INSPECCAO_MODO está em BLOQUEIO; regularize a inspeção ou aguarde a liberação (em modo ALERTA a viagem prossegue com alerta).");

/// <summary>Motorista inexistente/inativo para o tenant atual (HTTP 404, código MOTORISTA_INEXISTENTE; BR-4.2).</summary>
public sealed class TransporteMotoristaInexistenteException(long MotoristaId)
    : Exception($"MOTORISTA_INEXISTENTE: o motorista {MotoristaId} não existe em sigov.educacao_transporte_motorista para este tenant (ou está inativo); cadastre/ative o motorista antes de alocá-lo em viagem.");

/// <summary>Habilitação do motorista vencida na data da viagem (HTTP 409, código MOTORISTA_HABILITACAO_VENCIDA; BR-4.2).</summary>
public sealed class TransporteMotoristaHabilitacaoVencidaException(long MotoristaId, DateOnly Validade, DateOnly DataViagem)
    : Exception($"MOTORISTA_HABILITACAO_VENCIDA: a habilitação do motorista {MotoristaId} venceu em {Validade:yyyy-MM-dd}, anterior à data da viagem {DataViagem:yyyy-MM-dd}; renove a licença antes de alocar o motorista.");

/// <summary>Categoria de habilitação incompatível com a classe do veículo (HTTP 409, código MOTORISTA_CATEGORIA_INCOMPATIVEL; BR-4.2, tabela transporte_categoria_compatibilidade).</summary>
public sealed class TransporteMotoristaCategoriaIncompativelException(string Categoria, string Classe)
    : Exception($"MOTORISTA_CATEGORIA_INCOMPATIVEL: a categoria {Categoria} da habilitação não está habilitada para operar veículos da classe {Classe} segundo o catálogo sigov.transporte_categoria_compatibilidade (configurável; regra 11).");

/// <summary>Viagem sem monitor quando exigido pela esfera do tenant (HTTP 409, código VIAGEM_MONITOR_OBRIGATORIO; BR-4.3, parâmetro TRANSPORTE_MONITOR_OBRIGATORIO).</summary>
public sealed class TransporteViagemMonitorObrigatorioException()
    : Exception("VIAGEM_MONITOR_OBRIGATORIO: o parâmetro do tenant TRANSPORTE_MONITOR_OBRIGATORIO está ativo para esta esfera/jurisdicação; toda viagem exige ao menos um monitor (informe monitorIds no lançamento).");

/// <summary>Vinculados ativos da rota excedem a capacidade do veículo (HTTP 409, código VIAGEM_CAPACIDADE_EXCEDIDA; BR-4.3).</summary>
public sealed class TransporteViagemCapacidadeExcedidaException(int Capacidade, int Vinculados)
    : Exception($"VIAGEM_CAPACIDADE_EXCEDIDA: a rota possui {Vinculados} aluno(s) vinculado(s) ativo(s), acima da capacidade de {Capacidade} assentos do veículo; reduza os vínculos da rota ou use um veículo maior.");

/// <summary>Já existe viagem vigente do mesmo veículo na mesma data (HTTP 409, código VIAGEM_VEICULO_SOBREPOSICAO; backstop ux_transporte_viagem_tenant_veiculo_data; BR-4.3).</summary>
public sealed class TransporteViagemVeiculoSobreposicaoException(long VeiculoId, DateOnly Data)
    : Exception($"VIAGEM_VEICULO_SOBREPOSICAO: já existe viagem vigente (PROGRAMADA/EM_ANDAMENTO) do veículo {VeiculoId} na data {Data:yyyy-MM-dd}; conclua, cancele ou substitua a viagem existente antes de programar outra.");

/// <summary>Já existe viagem vigente do mesmo motorista na mesma data (HTTP 409, código VIAGEM_MOTORISTA_SOBREPOSICAO; backstop ux_transporte_viagem_tenant_motorista_data; BR-4.3).</summary>
public sealed class TransporteViagemMotoristaSobreposicaoException(long MotoristaId, DateOnly Data)
    : Exception($"VIAGEM_MOTORISTA_SOBREPOSICAO: já existe viagem vigente (PROGRAMADA/EM_ANDAMENTO) do motorista {MotoristaId} na data {Data:yyyy-MM-dd}; conclua, cancele ou substitua a viagem existente antes de programar outra.");

/// <summary>Ocorrência grave (ALTA) do veículo dentro da janela de carona ainda desfechada/recente (HTTP 409, código VIAGEM_OCORRENCIA_GRAVE_CARONA; BR-4.5, parâmetro TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS).</summary>
public sealed class TransporteViagemOcorrenciaGraveCaronaException(long VeiculoId, long OcorrenciaId, int Horas)
    : Exception($"VIAGEM_OCORRENCIA_GRAVE_CARONA: o veículo {VeiculoId} teve ocorrência grave (ALTA) {OcorrenciaId} e ainda está dentro da janela de carona de {Horas}h após o desfecho (relatório); desfeche a ocorrência e aguarde a janela paramétrica (TRANSPORTE_OCORRENCIA_GRAVE_CARONA_HORAS) expirar.");

/// <summary>Viagem inexistente/inativa para o tenant atual (HTTP 404, código TRANSPORTE_VIAGEM_INEXISTENTE).</summary>
public sealed class TransporteViagemInexistenteException(long ViagemId)
    : Exception($"TRANSPORTE_VIAGEM_INEXISTENTE: a viagem {ViagemId} não existe em sigov.educacao_transporte_viagem para este tenant (ou foi excluída).");

/// <summary>Ciclo da viagem fora de ordem na operação (HTTP 409, código VIAGEM_ESTADO_INVALIDO; BR-4.7).</summary>
public sealed class TransporteViagemEstadoInvalidoException(long ViagemId, string Operacao, string StatusAtual)
    : Exception($"VIAGEM_ESTADO_INVALIDO: a operação '{Operacao}' exige outra posição do ciclo da viagem {ViagemId} (status atual {StatusAtual}); o ciclo canônico é PROGRAMADA -> EM_ANDAMENTO (início com hora) -> CONCLUIDA (fim com hora e km), com CANCELADA/SUBSTITUIDA terminais.");

/// <summary>Cancelamento após início sem ocorrência vinculada (HTTP 409, código VIAGEM_CANCELAMENTO_EXIGE_OCORRENCIA; BR-4.7).</summary>
public sealed class TransporteViagemCancelamentoExigeOcorrenciaException(long ViagemId, string StatusAtual)
    : Exception($"VIAGEM_CANCELAMENTO_EXIGE_OCORRENCIA: a viagem {ViagemId} já saiu de PROGRAMADA (status {StatusAtual}); o cancelamento agora exige uma ocorrência vinculada (informe ocorrenciaId, lançada nesta viagem com justificativa).");

/// <summary>Substituição sem informar novo veículo nem motorista (HTTP 400, código VIAGEM_SUBSTITUICAO_SEM_RECURSO; BR-4.7).</summary>
public sealed class TransporteViagemSubstituicaoSemRecursoException()
    : Exception("VIAGEM_SUBSTITUICAO_SEM_RECURSO: informe pelo menos um dos novos recursos (veiculoId ou motoristaId) e a justificativa da substituição; o recurso não informado permanece o original.");

/// <summary>Registro de presença inexistente/inativo no tenant (HTTP 404, código TRANSPORTE_PRESENCIA_INEXISTENTE; BR-4.4).</summary>
public sealed class TransportePresencaInexistenteException(long PresencaId)
    : Exception($"TRANSPORTE_PRESENCIA_INEXISTENTE: o registro de presença {PresencaId} não existe em sigov.educacao_transporte_frequencia (tipo PRESENCA_TRANSPORTE) para este tenant.");

/// <summary>Aluno sem vínculo ativo na rota da viagem no lançamento de presença (HTTP 409, código PRESENCIA_ALUNO_FORA_DA_ROTA; BR-4.4).</summary>
public sealed class TransportePresencaAlunoForaDaRotaException(long AlunoId, long RotaId)
    : Exception($"PRESENCA_ALUNO_FORA_DA_ROTA: o aluno {AlunoId} não possui vínculo ativo com a rota {RotaId} da viagem; lances presença apenas de alunos vinculados à rota (vínculo é o domínio de origem da presença do transporte).");

/// <summary>Status de presença fora do conjunto canônico (HTTP 400, código PRESENCIA_STATUS_INVALIDO; BR-4.4).</summary>
public sealed class TransportePresencaStatusInvalidoException(string Status)
    : Exception($"PRESENCIA_STATUS_INVALIDO: status '{Status}' inválido; os estados canônicos do dia são PRESENTE, FALTANTE e DESLIGADO.");

/// <summary>Presença fechada: novo lançamento só do registro estornado (HTTP 409, código PRESENCAS_FECHADAS_ESTORNO_OBRIGATORIO; BR-4.4).</summary>
public sealed class TransportePresencasFechadasEstornoObrigatorioException(long ViagemId, long AlunoId)
    : Exception($"PRESENCAS_FECHADAS_ESTORNO_OBRIGATORIO: as presenças da viagem {ViagemId} estão fechadas; para relançar o aluno {AlunoId}, estorne antes o registro existente com justificativa (estorno desbloqueia um registro por vez).");

/// <summary>Estorno de presença sem justificativa (HTTP 400, código PRESENCIA_ESTORNO_JUSTIFICATIVA_OBRIGATORIA; BR-4.4).</summary>
public sealed class TransportePresencaEstornoJustificativaObrigatorioException()
    : Exception("PRESENCIA_ESTORNO_JUSTIFICATIVA_OBRIGATORIA: todo estorno de presença de transporte exige justificativa descrevendo o motivo da desbloqueação do registro.");

/// <summary>Fechamento de presença incompleto — vinculados sem registro (HTTP 409, código PRESENCAS_INCOMPLETAS; BR-4.4).</summary>
public sealed class TransportePresencasIncompletasException(long ViagemId, int Vinculados, int Registrados)
    : Exception($"PRESENCAS_INCOMPLETAS: a viagem {ViagemId} tem {Vinculados} aluno(s) vinculado(s) na rota e apenas {Registrados} presença(s) registrada(s); lance a presença de todos os vinculados antes de fechar.");

/// <summary>Presença já fechada na viagem (HTTP 409, código PRESENCAS_JA_FECHADAS; BR-4.4).</summary>
public sealed class TransportePresencasJaFechadasException(long ViagemId)
    : Exception($"PRESENCAS_JA_FECHADAS: as presenças da viagem {ViagemId} já estão fechadas; estorne o registro específico com justificativa para alterá-lo (um registro por vez).");

/// <summary>Tipo de ocorrência de viagem fora do conjunto canônico (HTTP 400, código OCORRENCIA_TRANSPORTE_TIPO_INVALIDO; BR-4.5).</summary>
public sealed class TransporteOcorrenciaTipoInvalidoException(string Tipo)
    : Exception($"OCORRENCIA_TRANSPORTE_TIPO_INVALIDO: tipo '{Tipo}' inválido; os tipos canônicos de ocorrência de viagem são ACIDENTE, QUEBRA, CLIMA e ROTINEIRA_OUTRA.");

/// <summary>Gravidade de ocorrência de viagem fora do conjunto canônico (HTTP 400, código OCORRENCIA_TRANSPORTE_GRAVIDADE_INVALIDA; BR-4.5).</summary>
public sealed class TransporteOcorrenciaGravidadeInvalidaException(string Gravidade)
    : Exception($"OCORRENCIA_TRANSPORTE_GRAVIDADE_INVALIDA: gravidade '{Gravidade}' inválida; as gravidades canônicas são BAIXA, MEDIA e ALTA (ALTA muda a situação da viagem para OCORRENCIA_GRAVE e exige desfecho).");

/// <summary>Ocorrência de viagem sem descrição (HTTP 400, código OCORRENCIA_TRANSPORTE_DESCRICAO_OBRIGATORIA; BR-4.5).</summary>
public sealed class TransporteOcorrenciaDescricaoObrigatorioException()
    : Exception("OCORRENCIA_TRANSPORTE_DESCRICAO_OBRIGATORIA: toda ocorrência de viagem exige descrição do fato (acidente, quebra, clima ou outro).");

/// <summary>Ocorrência de viagem inexistente/inativa no tenant (HTTP 404, código TRANSPORTE_OCORRENCIA_INEXISTENTE; BR-4.5).</summary>
public sealed class TransporteOcorrenciaInexistenteException(long OcorrenciaId)
    : Exception($"TRANSPORTE_OCORRENCIA_INEXISTENTE: a ocorrência {OcorrenciaId} não existe em sigov.educacao_transporte_ocorrencia para este tenant.");

/// <summary>Desfecho de ocorrência grave sem relatório (HTTP 400, código OCORRENCIA_DESFECHO_RELATORIO_OBRIGATORIO; BR-4.5).</summary>
public sealed class TransporteOcorrenciaDesfechoRelatorioObrigatorioException()
    : Exception("OCORRENCIA_DESFECHO_RELATORIO_OBRIGATORIO: o desfecho da ocorrência exige o relatório do fato (resumo técnico/administrativo) que autoriza a liberação do veículo para novas viagens.");

/// <summary>Ocorrência já desfechada (HTTP 409, código OCORRENCIA_JA_DESFECHADA; BR-4.5).</summary>
public sealed class TransporteOcorrenciaJaDesfechadaException(long OcorrenciaId)
    : Exception($"OCORRENCIA_JA_DESFECHADA: a ocorrência {OcorrenciaId} já possui desfecho (relatório) registrado; o desfecho é único por ocorrência.");

/// <summary>Segunda ocorrência ALTA vigente na mesma viagem (HTTP 409, código OCORRENCIA_ALTA_VIGENTE; backstop ux_transporte_ocorrencia_tenant_viagem_grave; BR-4.5).</summary>
public sealed class TransporteOcorrenciaAltaVigenteException(long OcorrenciaId)
    : Exception($"OCORRENCIA_ALTA_VIGENTE: a viagem já possui uma ocorrência ALTA sem desfecho (ocorrência {OcorrenciaId}); desfeche-a com relatório antes de lançar outra ALTA.");

/// <summary>Valor de custo realizado inválido (HTTP 400, código CUSTO_REALIZADO_VALOR_INVALIDO; BR-4.6).</summary>
public sealed class TransporteCustoRealizadoValorInvalidoException(decimal Valor)
    : Exception($"CUSTO_REALIZADO_VALOR_INVALIDO: valor {Valor:0.####} inválido; o custo efetivo realizado deve ser positivo.");

/// <summary>Cancelamento sem justificativa (HTTP 400, código VIAGEM_CANCELAMENTO_JUSTIFICATIVA_OBRIGATORIA; BR-4.7).</summary>
public sealed class TransporteViagemCancelamentoJustificativaObrigatorioException()
    : Exception("VIAGEM_CANCELAMENTO_JUSTIFICATIVA_OBRIGATORIO: todo cancelamento de viagem exige justificativa descrevendo o motivo (libera veículo/motorista da data e entra na cronologia append-only).");

/// <summary>Custo realizado já registrado para a viagem (HTTP 409, código CUSTO_REALIZADO_EXISTENTE; backstop ux_transporte_custo_tenant_viagem_realizado; BR-4.6).</summary>
public sealed class TransporteCustoRealizadoExistenteException(long ViagemId)
    : Exception($"CUSTO_REALIZADO_EXISTENTE: a viagem {ViagemId} já possui custo REALIZADO registrado; o custo efetivo é imutável (sem edição manual) e o conjunto ESTIMADO permanece intocado para comparação no relatório.");
