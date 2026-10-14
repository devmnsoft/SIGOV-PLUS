namespace Sigov.Application.Rh.Previdencia;

// RC-EVO Ciclo 2 Sprint 12 (BR-12.1 a BR-12.8): estacao de folha e previdencia publica
// multi-esfera com o maximo de regras da legislacao brasileira parametrizadas no banco
// (CF/88 art. 37 XV/XVI e 40; EC 103/2019; LC 51/1984; Lei 8.112/1990; CLT arts. 129-151;
// Lei 4.749/65 c/c Lei 12.735/2012; Decreto 3.048/99; Portaria MTP 672/2019). Falhas
// nomeadas explicitas (regra 13); autorizacao fail-closed por chave pontuada (regra 11).

public sealed record PrevidenciaContext(long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string CorrelationId);

// ------------------------------- REQUESTS --------------------------------------

public sealed record CargoCriarRequest(
    string CodigoExterno, string Nome, string? Classe, string? Padrao, string? Nivel,
    string ModoRemuneracao, decimal ValorReferencia);

public sealed record ServidorAdmitirRequest(
    string MatriculaFuncional, string Nome, string CargoCodigo, DateOnly DataAdmissao,
    string TipoVinculo, string? Uf);

public sealed record RemuneracaoRegistrarRequest(long VinculoId, string NaturezaCodigo, decimal Valor, bool Alterar = false);

public sealed record DesligamentoRequest(long VinculoId, DateOnly DataDesligamento, string Motivo);

public sealed record FolhaCompetenciaRequest(DateOnly Competencia, DateOnly DataFixa);

public sealed record FolhaReabrirRequest(DateOnly Competencia, DateOnly DataFixa, string Justificativa);

public sealed record ReprocessarCalculoRequest(DateOnly Competencia, long VinculoId, DateOnly DataFixa, string Justificativa);

public sealed record FeriasVinculoRequest(long VinculoId, DateOnly DataFixa);

public sealed record FeriasProgramarRequest(
    long VinculoId, DateOnly DataFixa, DateOnly DataInicio, DateOnly DataFim, int DiasGoze, int DiasAbono);

public sealed record FeriasGozoRequest(long ProgramacaoId, DateOnly DataFixa);

public sealed record AfastamentoRegistrarRequest(
    long VinculoId, string Tipo, DateOnly DataFixa, DateOnly DataInicio, DateOnly DataFim,
    string DocumentoGed, string RegimeOpcao);

public sealed record AfastamentoDevolverRequest(long AfastamentoId, DateOnly DataFixa, string Motivo);

public sealed record EsocialLoteGerarRequest(DateOnly Competencia, string TipoLote, DateOnly DataFixa);

// ------------------------------- DTOs ------------------------------------------

public sealed record CargoDto(long Id, string CodigoExterno, string Nome, string? Classe, string? Padrao, string? Nivel, string ModoRemuneracao, decimal ValorReferencia, bool Ativo);

public sealed record ServidorDto(long Id, string MatriculaFuncional, string Nome, string EsferaGoverno, string? Uf, long VinculoId, string SituacaoVinculo, string TipoVinculo, DateOnly DataAdmissao, string CargoCodigo, string CargoNome, decimal VencimentoBase);

public sealed record RemunItemDto(long Id, long VinculoId, string NaturezaCodigo, decimal Valor, bool IncidePrevidencia, bool IncideIrrf);

public sealed record FolhaCompetenciaDto(long Id, DateOnly Competencia, string Status, DateTime? FechadaEm, string? JustificativaReabertura, int CalculosVigentes);

public sealed record CalculoResumo(decimal Bruto, decimal BasePrevidencia, decimal InssValor, decimal BaseIrrf, decimal IrrfValor, decimal DecimoValor, decimal Liquido);

public sealed record FolhaFechamentoDto(long CompetenciaId, DateOnly Competencia, string Status, bool Idempotente, int CalculosGerados, int TotalCalculos, CalculoResumo Totais);

public sealed record CalculoItemDto(int Sequencia, string Grupo, string Descricao, string? NaturezaCodigo, decimal? Base, decimal? AliquotaPct, decimal Valor, string? Formula);

public sealed record CalculoDto(long Id, long VinculoId, DateOnly Competencia, int Versao, DateOnly DataBaseCalculo, string Status, string? MotivoVeto, CalculoResumo Valores, IReadOnlyList<CalculoItemDto> Itens);

public sealed record CalculoVersaoDto(long Id, int Versao, string Status, DateOnly DataBaseCalculo, string? MotivoVeto, DateTime? VetadaEm);

public sealed record ContrachequeDto(
    string MatriculaFuncional, string Nome, string EsferaGoverno, string CargoCodigo, string CargoNome, string? Classe,
    DateOnly Competencia, int Versao, DateOnly DataBaseCalculo, string Status,
    decimal Bruto, decimal BasePrevidencia, decimal InssValor, decimal BaseIrrf, decimal IrrfValor, decimal DecimoValor, decimal Liquido,
    IReadOnlyList<CalculoItemDto> Itens);

public sealed record FeriasCicloDto(long Id, DateOnly InicioAquisitivo, DateOnly FimAquisitivo, int DiasAdquiridos, int DiasGozados, int DiasAbonados, int SaldoDias);

public sealed record FeriasApuracaoDto(long VinculoId, int CiclosCriados, int TotalCiclos, int SaldoTotalDias, IReadOnlyList<FeriasCicloDto> Ciclos);

public sealed record FeriasProgramacaoDto(long Id, long FeriasId, DateOnly DataInicio, DateOnly DataFim, int DiasGoze, int DiasAbono, decimal SalarioDia, decimal AdicionalTerco, string Status, int SaldoRestanteDias);

public sealed record AfastamentoDto(long Id, long VinculoId, string Tipo, string RegimeOpcao, DateOnly DataInicio, DateOnly DataFim, int Dias, string DocumentoGed, string Status, DateTime? DevolvidoEm, string? MotivoDevolucao);

public sealed record DecimoSituacaoDto(long VinculoId, int Ano, string Status, int? MesesAte1aParcela, int? MesesFinais, decimal? Parcela1Valor, decimal? Parcela2Valor);

public sealed record ParametroEfetivoDto(string Codigo, string Nome, string Tipo, string ValorEfetivo, string Origem, string? Descricao);

public sealed record EsocialEventoDto(long Id, int Sequencia, string Evento, string AlvoTexto, bool ValidacaoOk, IReadOnlyList<string> Mensagens);

public sealed record EsocialLoteDto(long Id, DateOnly Competencia, string TipoLote, int Versao, string Status, int TotalEventos, int EventosComErro, DateTime GeradoEm, DateTime? EnviadoEm);

public sealed record EsocialLoteDetalheDto(EsocialLoteDto Lote, IReadOnlyList<EsocialEventoDto> Eventos);

public sealed record DashboardPrevidenciaDto(
    string? EsferaGoverno, int CargosAtivos, int ServidoresAtivos, int CompetenciasFechadas,
    int CalculosVigentes, int FeriasCiclosComSaldo, int AfastamentosAtivos, int LotesEsocialGerados, int LotesEsocialValidados, int LotesEsocialBloqueados,
    decimal TotalLiquidoUltimaFolha);

// --------------------------- FALHAS NOMEADAS (regra 13) -------------------------
// 404 - recurso ausente / autoridade inexistente
public class PrevidenciaRecursoNaoDisponivelException(string causa) : Exception($"PREVIDENCIA_NAO_DISPONIVEL: schema/autoridade ausente ({causa}).") { }
public class CargoInexistenteException : Exception { public CargoInexistenteException() : base("PREVIDENCIA_CARGO_INEXISTENTE: cargo nao encontrado para o tenant.") { } }
public class ServidorInexistenteException : Exception { public ServidorInexistenteException() : base("PREVIDENCIA_SERVIDOR_INEXISTENTE: servidor nao encontrado para o tenant.") { } }
public class VinculoInexistenteException : Exception { public VinculoInexistenteException() : base("PREVIDENCIA_VINCULO_INEXISTENTE: vinculo nao encontrado para o tenant.") { } }
public class FolhaCompetenciaInexistenteException : Exception { public FolhaCompetenciaInexistenteException() : base("PREVIDENCIA_FOLHA_INEXISTENTE: competencia de folha nao existe para o tenant.") { } }
public class FeriasCicloInexistenteException : Exception { public FeriasCicloInexistenteException() : base("PREVIDENCIA_FERIAS_SEM_SALDO: nenhum ciclo aquisitivo com saldo positivo e concluido.") { } }
public class AfastamentoInexistenteException : Exception { public AfastamentoInexistenteException() : base("PREVIDENCIA_AFASTAMENTO_INEXISTENTE: afastamento nao encontrado para o tenant.") { } }
public class LoteEsocialInexistenteException : Exception { public LoteEsocialInexistenteException() : base("PREVIDENCIA_LOTE_INEXISTENTE: lote eSocial nao encontrado para o tenant.") { } }
public class CalculoVigenteInexistenteException : Exception { public CalculoVigenteInexistenteException() : base("PREVIDENCIA_CALCULO_INEXISTENTE: nao ha calculo VIGENTE para o vinculo x competencia.") { } }
public class NaturezaRemuneracaoInexistenteException(string codigo) : Exception($"PREVIDENCIA_NATUREZA_INEXISTENTE: natureza '{codigo}' nao esta catalogada no banco.") { }

// 400 - validacao de entrada / configuracao ausente
public class EntidadeSemEsferaException : Exception { public EntidadeSemEsferaException() : base("ESFERA_NAO_CONFIGURADA: a entidade do contexto institucional nao tem esfera_governo definida (sigov.entidade).") { } }
public class CompetenciaInvalidaException : Exception { public CompetenciaInvalidaException() : base("COMPETENCIA_INVALIDA: a competencia deve ser o dia 1 do mes.") { } }
public class DataFixaInvalidaException : Exception { public DataFixaInvalidaException() : base("DATA_FIXA_INVALIDA: a data base do calculo deve ser dentro ou apos a competencia.") { } }
public class PeriodoInvalidoException : Exception { public PeriodoInvalidoException() : base("PERIODO_INVALIDO: data_fim anterior a data_inicio.") { } }
public class AfastamentoTipoInvalidoException : Exception { public AfastamentoTipoInvalidoException() : base("AFASTAMENTO_TIPO_INVALIDO: tipo fora do catalogo (SAUDE/ESTUDO/MATERNIDADE/PATERNIDADE/ADOCAO).") { } }
public class DocumentoGedObrigatoriaException : Exception { public DocumentoGedObrigatoriaException() : base("DOCUMENTO_GED_OBRIGATORIO: todo afastamento exige documento-comprovante no GED (controle TCU).") { } }
public class AfastamentoDuracaoInconsistenteException(string tipo, int esperado, int real) : Exception($"DURACAO_AFASTAMENTO_INCONSISTENTE: {tipo} exige {esperado} dias (parametro) e o periodo informado tem {real}.") { }
public class ReaberturaSemJustificativaException : Exception { public ReaberturaSemJustificativaException() : base("REABERTURA_SEM_JUSTIFICATIVA: reabrir competencia fechada exige justificativa registrada.") { } }
public class ReprocessoSemJustificativaException : Exception { public ReprocessoSemJustificativaException() : base("REPROCESSO_SEM_JUSTIFICATIVA: reprocessar calculo exige justificativa registrada.") { } }
public class IntervaloFeriasInsuficienteException(int diasDisponiveis, int minimo) : Exception($"INTERVALO_FERIAS_INSUFICIENTE: aviso previo de {diasDisponiveis} dias abaixo do minimo parametrico ({minimo} dias - Lei 8.112/90 art. 84).") { }
public class FracaoFeriasInsuficienteException(int diasGoze, int minimo) : Exception($"FRACAO_FERIAS_INSUFICIENTE: gozo de {diasGoze} dias abaixo do minimo do periodo fracionado ({minimo} dias - CLT art. 143).") { }
public class AbonoPecuniarioMinimoException(int diasAbono, int minimo) : Exception($"ABONO_PECUNIARIO_INFERIOR: abono de {diasAbono} dias abaixo do minimo parametrico ({minimo} dias - CLT art. 145).") { }
public class SaldoFeriasInsuficienteException(int solicitado, int saldo) : Exception($"SALDO_FERIAS_INSUFICIENTE: solicitado {solicitado} dias contra saldo {saldo} dias.") { }
public class FracionamentoFeriasExcedidoException(int solicitados, int maximo) : Exception($"FRACIONAMENTO_MAXIMO_EXCEDIDO: {solicitados}o periodo excede o fracionamento maximo parametrico ({maximo} - Lei 8.112/90).") { }
public class DevolucaoAfastamentoAntecipadaException : Exception { public DevolucaoAfastamentoAntecipadaException() : base("DEVOLUCAO_ANTICIPADA: o afastamento so e devolvido automaticamente quando a data base atinge a data_fim.") { } }

// 409 - conflito de estado / backstop
public class CargoJaExisteException : Exception { public CargoJaExisteException() : base("CARGO_JA_EXISTE: codigo_externo ja cadastrado para o tenant.") { } }
public class MatriculaFuncionalDuplicadaException : Exception { public MatriculaFuncionalDuplicadaException() : base("MATRICULA_FUNCIONAL_DUPLICADA: a matricula funcional ja pertence a outro servidor do tenant (backstop unico).") { } }
public class VinculoAtivoExisteException : Exception { public VinculoAtivoExisteException() : base("VINCULO_ATIVO_EXISTE: o servidor ja possui um vinculo ATIVO (backstop unico - um servidor, um vinculo ativo).") { } }
public class VinculoNaoAtivoException : Exception { public VinculoNaoAtivoException() : base("VINCULO_NAO_ATIVO: a operacao requer vinculo ATIVO.") { } }
public class RemuneracaoJaExisteException : Exception { public RemuneracaoJaExisteException() : base("REMUNERACAO_JA_EXISTE: natureza ja cadastrada no vinculo (use alterar=true para corrigir o valor).") { } }
public class FolhaJafecadaException : Exception { public FolhaJafecadaException() : base("FOLHA_JA_FECHADA: competencia ja esta FECHADA (reabra com justificativa antes de recalcular).") { } }
public class FolhaNaoEstavaFechadaException : Exception { public FolhaNaoEstavaFechadaException() : base("FOLHA_NAO_FECHADA: a competencia nao esta FECHADA, nada a reabrir.") { } }
public class ServidorAcimaDoTetoException : Exception { public ServidorAcimaDoTetoException(string matricula, decimal bruto, decimal teto, string esfera) : base($"SERVIDOR_ACIMA_TETO: matricula {matricula} com bruto {bruto:0.00} acima do teto constitucional da esfera ({teto:0.00}, esfera {esfera}) - CF/88 art. 37 XV/XVI c/c EC 103/2019; o fechamento permanece bloqueado.") { } }
public class SemCicloAquisitivoNovoException : Exception { public SemCicloAquisitivoNovoException() : base("SEM_NOVO_CICLO_AQUISITIVO: nenhum ciclo aquisitivo de 12 meses novo se concluiu ate a data base.") { } }
public class FeriasProgramacaoNaoProgramadaException : Exception { public FeriasProgramacaoNaoProgramadaException() : base("PROGRAMACAO_ESTADO_INVALIDO: apenas programacao PROGRAMADA pode registrar gozo.") { } }
public class AfastamentoSobrepostoException : Exception { public AfastamentoSobrepostoException() : base("AFASTAMENTO_SOBREPOSTO: ha afastamento ATIVO com periodo sobreposto no mesmo vinculo.") { } }
public class AfastamentoJaDevolvidoException : Exception { public AfastamentoJaDevolvidoException() : base("AFASTAMENTO_JA_DEVOLVIDO: o afastamento ja foi devolvido.") { } }
public class DecimoSemPrimeiraParcelaException : Exception { public DecimoSemPrimeiraParcelaException() : base("DECIMO_SEM_PARCELA1: a 2a parcela do 13o exige a 1a parcela registrada na competencia do mes parametrizado.") { } }
public class FolhaNaoFechadaParaLoteException : Exception { public FolhaNaoFechadaParaLoteException() : base("FOLHA_NAO_FECHADA_PARA_LOTE: o lote REMUNERACAO_S1200 exige competencia FECHADA com calculos vigentes.") { } }
public class LoteEsocialBloqueadoValidacaoException : Exception { public LoteEsocialBloqueadoValidacaoException(int erros) : base($"BLOQUEADO_VALIDACAO: {erros} evento(s) com erro de validacao - o envio foi bloqueado (sem sucesso simulado).") { } }

// ------------------------------- SERVICO ---------------------------------------

public interface IPrevidenciaFolhaService
{
    // BR-12.1 cadastro
    Task<CargoDto> CriarCargoAsync(PrevidenciaContext x, CargoCriarRequest r, CancellationToken ct);
    Task<IReadOnlyList<CargoDto>> ListarCargosAsync(PrevidenciaContext x, CancellationToken ct);
    Task<ServidorDto> AdmitirServidorAsync(PrevidenciaContext x, ServidorAdmitirRequest r, CancellationToken ct);
    Task<IReadOnlyList<ServidorDto>> ListarServidoresAsync(PrevidenciaContext x, CancellationToken ct);
    Task<RemunItemDto> RegistrarRemuneracaoAsync(PrevidenciaContext x, RemuneracaoRegistrarRequest r, CancellationToken ct);
    Task<long> DesligarServidorAsync(PrevidenciaContext x, DesligamentoRequest r, CancellationToken ct);

    // BR-12.2/12.5/12.6 folha deterministica + teto
    Task<FolhaCompetenciaDto> AbrirCompetenciaAsync(PrevidenciaContext x, FolhaCompetenciaRequest r, CancellationToken ct);
    Task<FolhaFechamentoDto> FecharFolhaAsync(PrevidenciaContext x, FolhaCompetenciaRequest r, CancellationToken ct);
    Task<FolhaCompetenciaDto> ReabrirFolhaAsync(PrevidenciaContext x, FolhaReabrirRequest r, CancellationToken ct);

    // BR-12.3 reprocesso versionado
    Task<CalculoDto> ReprocessarCalculoAsync(PrevidenciaContext x, ReprocessarCalculoRequest r, CancellationToken ct);
    Task<IReadOnlyList<CalculoVersaoDto>> ListarCalculosAsync(PrevidenciaContext x, long vinculoId, DateOnly competencia, CancellationToken ct);

    // BR-12.7 contracheque
    Task<ContrachequeDto> ObterContrachequeAsync(PrevidenciaContext x, long vinculoId, DateOnly competencia, CancellationToken ct);

    // BR-12.4 ferias + afastamentos
    Task<FeriasApuracaoDto> ApurarAquisicaoFeriasAsync(PrevidenciaContext x, FeriasVinculoRequest r, CancellationToken ct);
    Task<FeriasProgramacaoDto> ProgramarFeriasAsync(PrevidenciaContext x, FeriasProgramarRequest r, CancellationToken ct);
    Task<long> RegistrarGozoFeriasAsync(PrevidenciaContext x, FeriasGozoRequest r, CancellationToken ct);
    Task<AfastamentoDto> RegistrarAfastamentoAsync(PrevidenciaContext x, AfastamentoRegistrarRequest r, CancellationToken ct);
    Task<AfastamentoDto> DevolverAfastamentoAsync(PrevidenciaContext x, AfastamentoDevolverRequest r, CancellationToken ct);

    // 13o salario
    Task<DecimoSituacaoDto> SituacaoDecimoAsync(PrevidenciaContext x, long vinculoId, int ano, CancellationToken ct);

    // parametros
    Task<IReadOnlyList<ParametroEfetivoDto>> ParametrosEfetivosAsync(PrevidenciaContext x, CancellationToken ct);

    // BR-12.8 eSocial publico
    Task<EsocialLoteDto> GerarLoteEsocialAsync(PrevidenciaContext x, EsocialLoteGerarRequest r, CancellationToken ct);
    Task<EsocialLoteDto> EnviarLoteEsocialAsync(PrevidenciaContext x, long loteId, CancellationToken ct);
    Task<EsocialLoteDetalheDto> DetalharLoteEsocialAsync(PrevidenciaContext x, long loteId, CancellationToken ct);

    Task<DashboardPrevidenciaDto> DashboardAsync(PrevidenciaContext x, CancellationToken ct);
}
