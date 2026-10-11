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
