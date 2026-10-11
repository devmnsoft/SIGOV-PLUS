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
