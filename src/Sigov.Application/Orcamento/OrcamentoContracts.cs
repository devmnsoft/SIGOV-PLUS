using Sigov.Application.Rh.Previdencia;
using ParametroEfetivoDto = Sigov.Application.Rh.Previdencia.ParametroEfetivoDto;

namespace Sigov.Application.Orcamento;

// RC-EVO Ciclo 2 Sprint 13 (BR-13.1 a BR-13.8): estacao de orcamento e responsabilidade
// fiscal multi-esfera com a estrutura da Lei 4.320/64 e os limites/prestacoes de contas da
// LC 101/2000 parametrizados no banco (art. 20 limites de pessoal por poder; restos a pagar;
// art. 51 previsao bimestral; art. 54 RREO) e os minimos constitucionais de saude e educacao
// por esfera (CF/88 art. 212 - MDE 25% municipal/estadual; federal sem minimo = zero,
// parametrizavel). Banco = autoridade (regras 11/12): limites, percentuais e prazos existem
// apenas nas tabelas/parametros; nada hardcoded no codigo. Falhas nomeadas explicitas
// (regra 13); autorizacao fail-closed por chave pontuada (regra 11).
// Regras centrais: disponibilidade(dota) = inicial + creditos adicionais - reducoes -
// empenhado (SEM rollover entre exercicios); empenho CP so abre com disponibilidade
// suficiente (EMPENHO_ORCAMENTO_INSUFICIENTE); liquidacao SL <= empenho disponivel por
// item (LIQUIDACAO_SUPER_EMPENHO); exercicio ENCERRADO bloqueia movimentos novos
// (EXERCICIO_ENCERRADO); bloqueio LRF nasce do percentual pos-movimento sobre os dados
// reais (LRF_LIMITE_BLOQUEADO).

public sealed record OrcamentoContext(long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string CorrelationId);

// ------------------------------- REQUESTS --------------------------------------

public sealed record ProgramaCriarRequest(string Codigo, string Nome, string Objetivo);

public sealed record AcaoCriarRequest(
    string Codigo, string Nome, string Tipo, long? ProgramaId,
    string SubfuncaoCodigo, string FuncaoCodigo, string Poder,
    string CategoriaEconomica, string GrupoDespesa);

public sealed record DotacaoCriarRequest(long AcaoId, string FonteRecursoCodigo, decimal DotacaoInicial);

public sealed record CreditoRegistrarRequest(long DotacaoId, string Tipo, decimal Valor, string AtoLegal, string Justificativa, string ChaveIdempotencia);

public sealed record EmpenhoAbrirRequest(
    string Numero, DateOnly DataEmpenho, long? FornecedorPessoaId, string TipoEmpenho,
    string Historico, string ChaveIdempotencia, IReadOnlyList<EmpenhoItemRequest> Itens);

public sealed record EmpenhoItemRequest(long DotacaoId, decimal Valor);

public sealed record EmpenhoAnularRequest(long EmpenhoId, string Justificativa);

public sealed record LiquidacaoRegistrarRequest(
    string Numero, DateOnly DataLiquidacao, long EmpenhoId, string Historico,
    string ChaveIdempotencia, IReadOnlyList<LiquidacaoItemRequest> Itens);

public sealed record LiquidacaoItemRequest(long EmpenhoItemId, decimal Valor);

public sealed record LiquidacaoAnularRequest(long LiquidacaoId, string Justificativa);

public sealed record ReceitaCriarRequest(string Codigo, string Nome, string Natureza, decimal ValorPrevisto);

public sealed record ReceitaArrecadarRequest(long ReceitaId, decimal Valor, DateOnly DataArrecadacao, string Historico, string ChaveIdempotencia);

public sealed record LrfApurarRequest(DateOnly Periodo);

public sealed record PrevisaoGerarRequest(int Bimestre, string? Observacao);

public sealed record RreoGerarRequest(string Tipo, int? Semestre, DateOnly PeriodoFim);

public sealed record ExercicioEncerrarRequest(long ExercicioId, string Motivo);

public sealed record ExercicioReabrirRequest(long ExercicioId, string Justificativa);

// ---------------------------------- DTOs ---------------------------------------

public sealed record FuncaoDto(string Codigo, string Nome, bool FuncaoSocial, bool AreaSaudeEducacao, bool Ativo);

public sealed record SubfuncaoDto(string Codigo, string Nome, string FuncaoCodigo, bool Ativo);

public sealed record CatalogoFuncionalDto(IReadOnlyList<FuncaoDto> Funcoes, IReadOnlyList<SubfuncaoDto> Subfuncoes);

public sealed record ProgramaDto(long Id, string Codigo, string Nome, string Objetivo, string Situacao);

public sealed record AcaoDto(
    long Id, string Codigo, string Nome, string Tipo, long? ProgramaId,
    string SubfuncaoCodigo, string FuncaoCodigo, string Poder,
    string CategoriaEconomica, string GrupoDespesa, bool Ativo);

public sealed record DotacaoDto(
    long Id, long AcaoId, string AcaoCodigo, string AcaoNome, string SubfuncaoCodigo, string FuncaoCodigo,
    string Poder, string FonteRecursoCodigo, decimal DotacaoInicial, decimal CreditosSuplementares,
    decimal CreditosEspeciais, decimal CreditosExtraordinarios, decimal Reducoes, decimal Autorizado,
    decimal Empenhado, decimal Liquidado, decimal Pago, decimal Disponivel);

public sealed record CreditoAdicionalDto(long Id, long DotacaoId, string Tipo, decimal Valor, string AtoLegal, string Justificativa, string Situacao, DateTime CriadoEm, bool Idempotente);

public sealed record EmpenhoItemDto(long Id, long DotacaoId, string AcaoCodigo, string AcaoNome, string FonteRecursoCodigo, decimal Valor, decimal ValorAnulado, decimal ValorLiquidado, decimal Disponivel);

public sealed record EmpenhoDto(
    long Id, string Numero, DateOnly DataEmpenho, long? FornecedorPessoaId, string? FornecedorNome,
    string TipoEmpenho, string Historico, decimal ValorTotal, decimal ValorAnulado, decimal ValorLiquidado,
    decimal Disponivel, string Situacao, DateTime CriadoEm, bool Idempotente);

public sealed record EmpenhoDetalheDto(EmpenhoDto Empenho, IReadOnlyList<EmpenhoItemDto> Itens);

public sealed record LiquidacaoItemDto(long Id, long EmpenhoItemId, string AcaoCodigo, string AcaoNome, string FonteRecursoCodigo, decimal Valor);

public sealed record LiquidacaoDto(
    long Id, string Numero, DateOnly DataLiquidacao, long EmpenhoId, string EmpenhoNumero,
    string Historico, decimal ValorTotal, decimal ValorAnulado, string Situacao, DateTime CriadoEm, bool Idempotente);

public sealed record LiquidacaoDetalheDto(LiquidacaoDto Liquidacao, IReadOnlyList<LiquidacaoItemDto> Itens);

public sealed record ReceitaPrevistaDto(long Id, string Codigo, string Nome, string Natureza, decimal ValorPrevista, decimal ValorArrecadado, decimal SaldoAArrecadar, bool Idempotente);

public sealed record LimiteLrfDto(string Chave, string Nome, string TipoComparacao, decimal ValorAlvo, decimal AlertaPct, decimal? BloqueioPct, string BaseReceita, string Origem, bool Ativo);

public sealed record LrfApuracaoDto(long Id, DateOnly Periodo, string LimiteChave, string Nome, decimal BaseValor, decimal ValorAtual, decimal Percentual, string Situacao, string Detalhe, int Versao, DateTime CriadoEm);

public sealed record PrevisaoBimestralDto(
    long Id, int Bimestre, decimal ReceitaPrevistaTotal, decimal ReceitaArrecadadaTotal,
    decimal DespesaAutorizadaTotal, decimal DespesaEmpenhadaTotal, decimal DespesaLiquidadaTotal,
    decimal SaldoReceitas, bool Vigente, DateTime GeradoEm, string? Observacao);

public sealed record RreoLoteDto(long Id, string Tipo, int? Semestre, DateOnly PeriodoFim, bool Vigente, DateTime GeradoEm);

public sealed record RreoItemDto(string Conjunto, string Rotulo, decimal Valor, string Detalhe);

public sealed record RreoLoteDetalheDto(RreoLoteDto Lote, IReadOnlyList<RreoItemDto> Conjuntos);

public sealed record EstadoExercicioDto(long ExercicioId, int Ano, string Situacao, DateTime? EncerradoEm, string? EncerramentoMotivo, DateTime? ReabertoEm, string? ReaberturaJustificativa);

public sealed record DashboardOrcamentoDto(
    string? EsferaGoverno, int AcoesAtivas, int Dotacoes, int EmpenhosAtivos, int LiquidacoesAtivas,
    decimal TotalAutorizado, decimal TotalEmpenhado, decimal TotalLiquidado, decimal DisponivelTotal,
    decimal ReceitaPrevistaTotal, decimal ReceitaArrecadadaTotal, int LrfEmAlerta, int LrfEmBloqueio);

// --------------------------- FALHAS NOMEADAS (regra 13) -------------------------
// 404 - recurso ausente / autoridade inexistente
public class OrcamentoRecursoNaoDisponivelException(string causa) : Exception($"ORCAMENTO_NAO_DISPONIVEL: schema/autoridade ausente ({causa}).") { }
public class OrcamentoExercicioInexistenteException(int ano) : Exception($"ORCAMENTO_EXERCICIO_INEXISTENTE: exercicio {ano} nao existe para a entidade do contexto (registre em sigov.exercicio antes de operar o orcamento).") { }
public class OrcamentoProgramaInexistenteException : Exception { public OrcamentoProgramaInexistenteException() : base("ORCAMENTO_PROGRAMA_INEXISTENTE: programa nao encontrado para o exercicio/tenant.") { } }
public class OrcamentoAcaoInexistenteException : Exception { public OrcamentoAcaoInexistenteException() : base("ORCAMENTO_ACAO_INEXISTENTE: acao orcamentaria nao encontrada para o exercicio/tenant.") { } }
public class OrcamentoDotacaoInexistenteException : Exception { public OrcamentoDotacaoInexistenteException() : base("ORCAMENTO_DOTACAO_INEXISTENTE: dotacao nao encontrada para o exercicio/tenant.") { } }
public class OrcamentoEmpenhoInexistenteException : Exception { public OrcamentoEmpenhoInexistenteException() : base("ORCAMENTO_EMPENHO_INEXISTENTE: empenho nao encontrado para o exercicio/tenant.") { } }
public class OrcamentoEmpenhoItemInexistenteException : Exception { public OrcamentoEmpenhoItemInexistenteException() : base("ORCAMENTO_EMPENHO_ITEM_INEXISTENTE: item de empenho nao encontrado para o empenho/tenant.") { } }
public class OrcamentoLiquidacaoInexistenteException : Exception { public OrcamentoLiquidacaoInexistenteException() : base("ORCAMENTO_LIQUIDACAO_INEXISTENTE: liquidacao nao encontrada para o exercicio/tenant.") { } }
public class OrcamentoReceitaInexistenteException : Exception { public OrcamentoReceitaInexistenteException() : base("ORCAMENTO_RECEITA_INEXISTENTE: receita prevista nao encontrada para o exercicio/tenant.") { } }
public class OrcamentoSubfuncaoInexistenteException(string codigo) : Exception($"ORCAMENTO_SUBFUNCAO_INEXISTENTE: subfuncao '{codigo}' nao esta catalogada no banco (classificacao nacional).") { }
public class OrcamentoFuncaoInexistenteException(string codigo) : Exception($"ORCAMENTO_FUNCAO_INEXISTENTE: funcao '{codigo}' nao esta catalogada no banco (classificacao nacional).") { }
public class OrcamentoRreoInexistenteException : Exception { public OrcamentoRreoInexistenteException() : base("ORCAMENTO_RREO_INEXISTENTE: lote RREO nao encontrado para o exercicio/tenant.") { } }

// 400 - validacao de entrada / configuracao ausente
public class OrcamentoAcaoTipoInvalidoException(string tipo) : Exception($"ACAO_TIPO_INVALIDO: tipo '{tipo}' fora do catalogo (PROJETO/ATIVIDADE/SUBPROJETO - Lei 4.320/64).") { }
public class OrcamentoPoderInvalidoException(string poder) : Exception($"PODER_INVALIDO: poder '{poder}' fora do catalogo (EXECUTIVO/LEGISLATIVO/JUDICIARIO).") { }
public class OrcamentoCategoriaEconomicaInvalidaException(string categoria) : Exception($"CATEGORIA_ECONOMICA_INVALIDA: categoria '{categoria}' fora do catalogo (CORRENTE/INVESTIMENTO/AMORTIZACAO_DIVIDA/OUTRA_DESPESA).") { }
public class OrcamentoGrupoDespesaInvalidoException(string grupo) : Exception($"GRUPO_DESPESA_INVALIDO: grupo '{grupo}' fora do catalogo (PESSOAL_E_ENCARGOS/JUROS_E_ENCARGOS_DIVIDA/OUTRAS_DESPESAS_CORRENTES/INVESTIMENTOS/AMORTIZACAO_DA_DIVIDA/OUTRAS_DESPESAS_CAPITAIS).") { }
public class OrcamentoCreditoTipoInvalidoException(string tipo) : Exception($"CREDITO_TIPO_INVALIDO: tipo '{tipo}' fora do catalogo (SUPLEMENTAR/ESPECIAL/EXTRAORDINARIO - Lei 4.320/64 art. 41).") { }
public class OrcamentoEmpenhoTipoInvalidoException(string tipo) : Exception($"EMPENHO_TIPO_INVALIDO: tipo '{tipo}' fora do catalogo (CP/CP_GLOBAL/SUPLEMENTAR).") { }
public class OrcamentoReceitaNaturezaInvalidaException(string natureza) : Exception($"RECEITA_NATUREZA_INVALIDA: natureza '{natureza}' fora do catalogo (TRIBUTARIA/TRANSFERENCIAS/PATRIMONIAL/SERVICOS/DIVERSAS_RECEITAS).") { }
public class OrcamentoBimestreInvalidoException(int bimestre) : Exception($"BIMESTRE_INVALIDO: bimestre {bimestre} fora do intervalo 1..6 (LC 101/2000 art. 51).") { }
public class OrcamentoRreoTipoInvalidoException(string tipo) : Exception($"RREO_TIPO_INVALIDO: tipo '{tipo}' fora do catalogo (SEMESTRAL/ANUAL - LC 101/2000 art. 54).") { }
public class OrcamentoSemestreRreoInvalidoException(string tipo, int? semestre) : Exception($"RREO_SEMESTRE_INCONSISTENTE: para {tipo}, semestre deve ser {(tipo == "SEMESTRAL" ? "1 ou 2" : "ausente")}; recebido {semestre?.ToString() ?? "nulo"}.") { }
public class EmpenhoSemItensException : Exception { public EmpenhoSemItensException() : base("EMPENHO_SEM_ITENS: o empenho exige ao menos um item vinculado a uma dotacao.") { } }
public class LiquidacaoSemItensException : Exception { public LiquidacaoSemItensException() : base("LIQUIDACAO_SEM_ITENS: a liquidacao exige ao menos um item vinculado a um item de empenho.") { } }
public class OrcamentoJustificativaInsuficienteException(string ato, int tamanho, int minimo) : Exception($"JUSTIFICATIVA_INSUFICIENTE: {ato} exige justificativa com minimo de {minimo} caracteres (parametrizado); recebido(s) {tamanho}.") { }
public class OrcamentoArrecadacaoInvalidaException(decimal valor) : Exception($"ARRECADACAO_INVALIDA: valor {valor:0.00} deve ser maior que zero.") { }
public class OrcamentoRreoPeriodoInconsistenteException(int ano, DateOnly periodoFim) : Exception($"RREO_PERIODO_FIM_INCONSISTENTE: periodo_fim {periodoFim} fora do exercicio {ano}.") { }
// ESFERA_NAO_CONFIGURADA e demais falhas compartilhadas com a vertical RH reutilizam o
// contrato Sigov.Application.Rh.Previdencia (EntidadeSemEsferaException) - regra 11.

// 409 - conflito de estado / backstop
public class OrcamentoProgramaJaExisteException(string codigo) : Exception($"PROGRAMA_JA_EXISTE: codigo de programa '{codigo}' ja cadastrado no exercicio (backstop unico).") { }
public class OrcamentoAcaoJaExisteException(string codigo) : Exception($"ACAO_JA_EXISTE: codigo de acao '{codigo}' ja cadastrado no exercicio (backstop unico).") { }
public class OrcamentoDotacaoJaExisteException(string acaoCodigo, string fonte) : Exception($"DOTACAO_JA_EXISTE: a linha de dotacao ja existe para acao '{acaoCodigo}' x fonte '{fonte}' no exercicio (backstop unico).") { }
public class OrcamentoReceitaJaExisteException(string codigo) : Exception($"RECEITA_JA_EXISTE: codigo de receita prevista '{codigo}' ja cadastrado no exercicio (backstop unico).") { }
public class OrcamentoExercicioEncerradoException(int ano) : Exception($"EXERCICIO_ENCERRADO: o exercicio {ano} esta ENCERRADO - empenho, liquidacao e credito novos sao bloqueados (reabra com justificativa).") { }
public class OrcamentoExercicioNaoEstavaEncerradoException : Exception { public OrcamentoExercicioNaoEstavaEncerradoException() : base("EXERCICIO_NAO_ESTAVA_ENCERRADO: o exercicio nao esta ENCERRADO, nada a reabrir.") { } }
public class OrcamentoEmpenheiroInsuficienteException(string acaoCodigo, string fonte, decimal solicitado, decimal disponivel) : Exception($"EMPENHO_ORCAMENTO_INSUFICIENTE: acao '{acaoCodigo}' x fonte '{fonte}' - solicitado {solicitado:0.00} acima da disponibilidade ({disponivel:0.00}); disponibilidade = dotacao inicial + creditos adicionais - reducoes - empenhado (Lei 4.320/64).") { }
public class OrcamentoLiquidacaoSuperEmpenhoException(long empenhoItemId, decimal solicitado, decimal disponivel) : Exception($"LIQUIDACAO_SUPER_EMPENHO: item de empenho {empenhoItemId} - solicitado {solicitado:0.00} acima do empenho disponivel por item ({disponivel:0.00}).") { }
public class OrcamentoLrfLimiteBloqueadoException(string chave, string nome, decimal percentualPos, decimal bloqueioPct) : Exception($"LRF_LIMITE_BLOQUEADO: {chave} ({nome}) - o percentual apos o movimento ({percentualPos:0.##}%) atingiu o bloqueio parametrizado ({bloqueioPct:0.##}%); revise a dotacao, a receita ou o limite (LC 101/2000 / CF art. 212).") { }
public class OrcamentoEmpenhoJaAnuladoException : Exception { public OrcamentoEmpenhoJaAnuladoException() : base("EMPENHO_JA_ANULADO: o empenho ja esta ANULADO.") { } }
public class OrcamentoLiquidacaoJaAnuladaException : Exception { public OrcamentoLiquidacaoJaAnuladaException() : base("LIQUIDACAO_JA_ANULADA: a liquidacao ja esta ANULADA.") { } }
public class OrcamentoEmpenhoComLiquidacaoException(decimal liquidado) : Exception($"EMPENHO_COM_LIQUIDACAO: o empenho possui {liquidado:0.00} liquidados; anule as liquidacoes antes de anular o empenho.") { }
public class OrcamentoLiquidacaoSobreEmpenhoAnuladoException : Exception { public OrcamentoLiquidacaoSobreEmpenhoAnuladoException() : base("LIQUIDACAO_EMPENHO_ANULADO: o empenho referenciado esta ANULADO.") { } }

// ------------------------------- SERVICO ---------------------------------------

public interface IOrcamentoService
{
    // visao geral
    Task<DashboardOrcamentoDto> DashboardAsync(OrcamentoContext x, int ano, CancellationToken ct);

    // BR-13.1 estrutura: funcao -> subfuncao -> programa -> acao
    Task<CatalogoFuncionalDto> CatalogoFuncionalAsync(OrcamentoContext x, CancellationToken ct);
    Task<IReadOnlyList<ProgramaDto>> ListarProgramasAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<ProgramaDto> CriarProgramaAsync(OrcamentoContext x, int ano, ProgramaCriarRequest r, CancellationToken ct);
    Task<IReadOnlyList<AcaoDto>> ListarAcoesAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<AcaoDto> CriarAcaoAsync(OrcamentoContext x, int ano, AcaoCriarRequest r, CancellationToken ct);

    // BR-13.2 dotacao + creditos adicionais (sem rollover)
    Task<IReadOnlyList<DotacaoDto>> ListarDotacoesAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<DotacaoDto> CriarDotacaoAsync(OrcamentoContext x, int ano, DotacaoCriarRequest r, CancellationToken ct);
    Task<CreditoAdicionalDto> RegistrarCreditoAsync(OrcamentoContext x, int ano, CreditoRegistrarRequest r, CancellationToken ct);

    // BR-13.3 empenho CP
    Task<IReadOnlyList<EmpenhoDto>> ListarEmpenhosAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<EmpenhoDto> AbrirEmpenhoAsync(OrcamentoContext x, int ano, EmpenhoAbrirRequest r, CancellationToken ct);
    Task<EmpenhoDto> AnularEmpenhoAsync(OrcamentoContext x, int ano, EmpenhoAnularRequest r, CancellationToken ct);
    Task<EmpenhoDetalheDto> DetalharEmpenhoAsync(OrcamentoContext x, int ano, long empenhoId, CancellationToken ct);

    // BR-13.4 liquidacao SL
    Task<IReadOnlyList<LiquidacaoDto>> ListarLiquidacoesAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<LiquidacaoDto> RegistrarLiquidacaoAsync(OrcamentoContext x, int ano, LiquidacaoRegistrarRequest r, CancellationToken ct);
    Task<LiquidacaoDto> AnularLiquidacaoAsync(OrcamentoContext x, int ano, LiquidacaoAnularRequest r, CancellationToken ct);
    Task<LiquidacaoDetalheDto> DetalharLiquidacaoAsync(OrcamentoContext x, int ano, long liquidacaoId, CancellationToken ct);

    // receita prevista/arrecadada (base real das apuracoes)
    Task<IReadOnlyList<ReceitaPrevistaDto>> ListarReceitasAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<ReceitaPrevistaDto> CriarReceitaAsync(OrcamentoContext x, int ano, ReceitaCriarRequest r, CancellationToken ct);
    Task<ReceitaPrevistaDto> ArrecadarReceitaAsync(OrcamentoContext x, int ano, ReceitaArrecadarRequest r, CancellationToken ct);

    // BR-13.5 limites LRF (catalogo por esfera + apuracao versionada)
    Task<IReadOnlyList<LimiteLrfDto>> LimitesEfetivosAsync(OrcamentoContext x, CancellationToken ct);
    Task<IReadOnlyList<LrfApuracaoDto>> ApurarLrfAsync(OrcamentoContext x, int ano, LrfApurarRequest r, CancellationToken ct);
    Task<IReadOnlyList<LrfApuracaoDto>> ListarApuracoesAsync(OrcamentoContext x, int ano, CancellationToken ct);

    // BR-13.6 previsao bimestral automatica (LC 101/2000 art. 51)
    Task<PrevisaoBimestralDto> GerarPrevisaoBimestralAsync(OrcamentoContext x, int ano, PrevisaoGerarRequest r, CancellationToken ct);
    Task<IReadOnlyList<PrevisaoBimestralDto>> ListarPrevisoesAsync(OrcamentoContext x, int ano, CancellationToken ct);

    // BR-13.7 RREO semestral/anual (LC 101/2000 art. 54)
    Task<IReadOnlyList<RreoLoteDto>> ListarRreosAsync(OrcamentoContext x, int ano, CancellationToken ct);
    Task<RreoLoteDto> GerarRreoAsync(OrcamentoContext x, int ano, RreoGerarRequest r, CancellationToken ct);
    Task<RreoLoteDetalheDto> DetalharRreoAsync(OrcamentoContext x, int ano, long loteId, CancellationToken ct);

    // BR-13.8 competencia fechada
    Task<IReadOnlyList<EstadoExercicioDto>> ListarExerciciosAsync(OrcamentoContext x, CancellationToken ct);
    Task<EstadoExercicioDto> EncerrarExercicioAsync(OrcamentoContext x, ExercicioEncerrarRequest r, CancellationToken ct);
    Task<EstadoExercicioDto> ReabrirExercicioAsync(OrcamentoContext x, ExercicioReabrirRequest r, CancellationToken ct);

    // parametros
    Task<IReadOnlyList<ParametroEfetivoDto>> ParametrosEfetivosAsync(OrcamentoContext x, CancellationToken ct);
}
