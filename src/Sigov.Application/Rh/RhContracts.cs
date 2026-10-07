using Sigov.Application.Common;
using Sigov.Domain.Common;
using Sigov.Domain.Rh;

namespace Sigov.Application.Rh;

public static class RhPermissoes
{
    public const string Modulo = "rh";
    public const string Visualizar = "rh.registros.visualizar";
    public const string Criar = "rh.registros.criar";
    public const string Editar = "rh.registros.editar";
    public const string Excluir = "rh.registros.excluir";
    public const string Exportar = "rh.exportar";
    public const string Portal = "rh.portal.visualizar";
    public const string Dashboard = "rh.dashboard.visualizar";
    public const string IntegrarFinanceiro = "rh.financeiro.integrar";
    public const string Reabrir = "rh.apuracao.reabrir";
}

// Contratos exclusivos do Bloco 2. Os nomes prefixados evitam colisões no Swagger.
public sealed record RhPontoCriarJornadaRequest(string Nome, decimal CargaHoraria, TimeOnly Entrada, TimeOnly Saida, int ToleranciaMinutos = 0, string DiasSemana = "1,2,3,4,5");
public sealed record RhPontoCriarEscalaRequest(long ServidorId, long JornadaId, DateOnly PeriodoInicio, DateOnly? PeriodoFim);
public sealed record RhPontoRegistrarBatidaRequest(long ServidorId, DateTimeOffset DataHora, string Tipo, string Origem = "MANUAL", string? Justificativa = null);
public sealed record RhPontoCriarJustificativaRequest(long ServidorId, DateOnly DataReferencia, string Motivo);
public sealed record RhPontoApuracaoRequest(long ServidorId, DateOnly PeriodoInicio, DateOnly PeriodoFim);
public sealed record RhPontoHomologacaoRequest(string? JustificativaDivergencia = null);
public sealed record RhPontoIntegracaoFolhaRequest(long FolhaId);
public sealed record RhPontoReaberturaRequest(string? Justificativa = null);
public sealed record RhFeriasPeriodoAquisitivoDto(long ServidorId, DateOnly PeriodoInicio, DateOnly PeriodoFim);
public sealed record RhFeriasSolicitacaoRequest(long ServidorId, long PeriodoAquisitivoId, DateOnly PeriodoInicio, DateOnly PeriodoFim);
public sealed record RhFeriasAprovacaoRequest(string? Observacao = null);
public sealed record RhFeriasCancelamentoRequest(string Justificativa);
public sealed record RhCriarAfastamentoRequest(long ServidorId, long TipoId, DateOnly PeriodoInicio, DateOnly? PeriodoFim, string Motivo);
public sealed record RhAfastamentoAprovacaoRequest(string? Observacao = null);
public sealed record RhAfastamentoEncerramentoRequest(DateOnly DataEncerramento, string Motivo);
public sealed record RhPortalCriarSolicitacaoRequest(string Tipo, string Descricao);
public sealed record RhPortalAtualizacaoCadastralRequest(Dictionary<string, object?> Dados);
public sealed record RhPortalRespostaRequest(string Resposta);

public sealed record RhFiltro(int Page = 1, int PageSize = 20, string? Termo = null, bool? Ativo = null);
public sealed record RhRegistroCreateRequest(Dictionary<string, object?> Dados);
public sealed record RhRegistroUpdateRequest(Dictionary<string, object?> Dados, bool Ativo = true);
public sealed record RhRegistroResponse(long Id, string Recurso, Dictionary<string, object?> Dados, bool Ativo, DateTimeOffset CreatedAt, DateTimeOffset? UpdatedAt);
public sealed record RhDashboardResponse(long ServidoresAtivos, long VinculosAtivos, long FolhasAbertas, long FeriasProgramadas, long AfastamentosAtivos, decimal TotalFolhaMes);
public sealed record RhFinanceiroIntegracaoRequest(long FolhaId, DateOnly DataCompetencia, long? NaturezaDespesaId, long? FonteRecursoId, string Historico);
public sealed record RhPortalResumoResponse(long ServidorId, string Nome, IReadOnlyCollection<RhRegistroResponse> Contracheques, IReadOnlyCollection<RhRegistroResponse> Ferias, IReadOnlyCollection<RhRegistroResponse> Afastamentos);

// Apuração existente (lookup estruturado) usada para idempotência/reprocessamento §4.
public sealed record RhApuracaoExistenteDto(long Id, string Status, string? AnteriorDadosJson);

// RC-EVO-RH §5: decisão de justificativas/ajustes (origem preservada, transições nomeadas, invalidação de dependentes).
public sealed record RhRegistroComOrigemDto(long Id, string DadosJson, long? ServidorId, long? CriadoPor, DateTimeOffset CriadoEm, string Status);
public sealed record RhApuracaoJanelaDto(long Id, string Status);
public sealed record RhPontoAjusteResumoDto(long RegistroId, DateTimeOffset AjustadoEm, int ApuracoesInvalidadas, DateOnly JanelaInicio, DateOnly JanelaFim);
public sealed record RhJustificativaDecisaoDto(long JustificativaId, string Status, long? DecididoPor, int ApuracoesInvalidadas);

// RC-EVO-RH §6: homologação/reabertura (prévia com memória+pendências, versão revalidada, concorrência sem efeito duplicado).
public sealed record RhApuracaoHomologacaoResumoDto(long ApuracaoId, string Status, DateTimeOffset TransicaoEm, string VersaoRegras, IReadOnlyList<string> PendenciasGlobais, bool JaHomologada);
public sealed record RhApuracaoReaberturaResumoDto(long ApuracaoId, string Status, DateTimeOffset TransicaoEm, long? ReabertoPor, bool JaReaberta);

// RC-EVO-RH §7: integração da apuração homologada na folha (lançamentos materializados, unicidade origem→destino, retry idempotente).
public sealed record RhLancamentoPontoPayload(string RubricaCodigo, string RubricaNome, string Tipo, string Base, int QuantidadeBase, decimal Valor);
public sealed record RhIntegracaoLancamentoDto(long Id, string RubricaCodigo, string RubricaNome, string Tipo, string Base, int QuantidadeBase, decimal Valor);
public sealed record RhIntegracaoFolhaTx(long IntegracaoId, long? EventoId, IReadOnlyList<RhIntegracaoLancamentoDto> Lancamentos, decimal TotalProventos, decimal TotalDescontos, bool JaProcessada);
public sealed record RhIntegracaoFolhaResumoDto(long ApuracaoId, long FolhaId, long IntegracaoId, long? EventoFolhaId, string VersaoRegras, IReadOnlyList<RhIntegracaoLancamentoDto> Lancamentos, decimal TotalProventos, decimal TotalDescontos, decimal Liquido, IReadOnlyList<string> CriticasNaoBloqueantes, bool JaProcessada);

// RC-EVO-RH §8: portal com escopo próprio (vínculo usuário→servidor resolvido no servidor; totais recalculados).
public sealed record RhPortalVinculoDto(long ServidorId, string Nome);
public sealed record RhPortalCompetenciaFonte(
    long IntegracaoId, string StatusIntegracao, DateTimeOffset? ProcessadaEm,
    long? FolhaId, string? StatusFolha, int? AnoFolha, int? MesFolha,
    long? ApuracaoId, string? StatusApuracao, long? EventoFolhaId, string? VersaoRegras,
    DateOnly? PeriodoInicio, DateOnly? PeriodoFim, string? ApuracaoDadosJson,
    IReadOnlyList<string>? CriticasNaoBloqueantes, IReadOnlyList<RhIntegracaoLancamentoDto> Lancamentos,
    DateTimeOffset CriadoEm, DateTimeOffset? AtualizadoEm);
public sealed record RhPortalPendenciaItem(string Tipo, long Id, string Status, string? Descricao, DateTime Em);

public interface IRhRepository
{
    Task<PagedResult<RhRegistroResponse>> ListarAsync(long tenantId, string recurso, RhFiltro filtro, CancellationToken ct);
    Task<RhRegistroResponse?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct);
    Task<long> CriarAsync(long tenantId, string recurso, RhRegistroCreateRequest request, long? usuarioId, CancellationToken ct);
    Task AtualizarAsync(long tenantId, string recurso, long id, RhRegistroUpdateRequest request, long? usuarioId, CancellationToken ct);
    Task ExcluirAsync(long tenantId, string recurso, long id, long? usuarioId, CancellationToken ct);
    Task<RhDashboardResponse> DashboardAsync(long tenantId, CancellationToken ct);
    Task<RhPortalResumoResponse?> PortalServidorAsync(long tenantId, long servidorId, CancellationToken ct);
    Task<decimal> TotalLancamentosFolhaAsync(long tenantId, long folhaId, CancellationToken ct);
    Task<long> PrepararIntegracaoFinanceiraAsync(long tenantId, RhFinanceiroIntegracaoRequest request, long? usuarioId, CancellationToken ct);
    Task<byte[]> ExportarAsync(long tenantId, string recurso, string formato, CancellationToken ct);
    Task<bool> ExercicioAbertoAsync(long tenantId, long? exercicioId, CancellationToken ct);
    // RC-EVO-RH §4: apuração real de ponto (leitura das entradas + upsert idempotente)
    Task<IReadOnlyList<JornadaPontoRegra>> ListarJornadasAtivasAsync(long tenantId, CancellationToken ct);
    Task<IReadOnlyList<EscalaPontoResumo>> ListarEscalasPorServidorAsync(long tenantId, long servidorId, CancellationToken ct);
    Task<IReadOnlyList<BatidaPonto>> ListarBatidasPeriodoAsync(long tenantId, long servidorId, DateTimeOffset inicioUtc, DateTimeOffset fimUtc, CancellationToken ct);
    Task<IReadOnlyCollection<DateOnly>> ListarFeriadosPeriodoAsync(long tenantId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    Task<IReadOnlyCollection<DateOnly>> ListarAusenciasJustificadasAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    Task<string?> ObterFusoOperacaoAsync(CancellationToken ct);
    Task<RhApuracaoExistenteDto?> ObterApuracaoExistenteAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    Task<long> SalvarApuracaoPontoAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, string dadosJson, long? anteriorId, string? anteriorDadosJson, long? usuarioId, CancellationToken ct);
    // RC-EVO-RH §5: decisão de justificativas/ajustes (origem preservada + invalidação de apurações dependentes)
    Task<RhRegistroComOrigemDto?> ObterRegistroComOrigemAsync(long tenantId, string recurso, long id, CancellationToken ct);
    Task<IReadOnlyList<RhApuracaoJanelaDto>> ApuracoesCobertasPorJanelaAsync(long tenantId, long servidorId, DateOnly inicio, DateOnly fim, CancellationToken ct);
    Task<int> InvalidarApuracoesPorAjusteAsync(long tenantId, IReadOnlyCollection<long> ids, string motivo, long? usuarioId, CancellationToken ct);
    Task AtualizarComDeltaAsync(long tenantId, string recurso, long id, string deltaJson, string operacao, object? antes, object? depois, long? usuarioId, CancellationToken ct);
    // RC-EVO-RH §6: homologação/reabertura (update guardado por status + integrações de destino da apuração)
    Task<int> AtualizarStatusApuracaoGuardadoAsync(long tenantId, long id, string statusGuard, string novoStatus, string deltaJson, string operacao, object? antes, object? depois, string? justificativa, long? usuarioId, CancellationToken ct);
    Task<IReadOnlyList<RhRegistroComOrigemDto>> ListarIntegracoesFolhaDaApuracaoAsync(long tenantId, long apuracaoId, CancellationToken ct);
    // RC-EVO-RH §7: integração real da apuração homologada na folha (evento + lançamentos em transação única)
    Task<RhIntegracaoFolhaTx> IntegrarApuracaoNaFolhaAsync(long tenantId, long apuracaoId, long folhaId, long servidorId, DateOnly periodoInicio, DateOnly periodoFim, string versaoRegras, string resumoJson, string criticasJson, IReadOnlyList<RhLancamentoPontoPayload> lancamentos, long? usuarioId, CancellationToken ct);
    // RC-EVO-RH §8: portal com escopo próprio (vínculo sigov.rh_portal_usuario; filtro pelo próprio servidor)
    Task<RhPortalVinculoDto?> ServidorDoPortalAsync(long tenantId, long usuarioId, CancellationToken ct);
    Task<long> ContarCompetenciasPortalAsync(long tenantId, long servidorId, CancellationToken ct);
    Task<IReadOnlyList<RhPortalCompetenciaFonte>> ListarCompetenciasPortalAsync(long tenantId, long servidorId, int limite, int offset, CancellationToken ct);
    Task<IReadOnlyList<RhPortalPendenciaItem>> ListarPendenciasPortalAsync(long tenantId, long servidorId, int limite, CancellationToken ct);
    Task<PagedResult<RhRegistroResponse>> ListarPorServidorAsync(long tenantId, string recurso, long servidorId, RhFiltro filtro, CancellationToken ct);
}

public interface IRhService
{
    Task<Result<PagedResult<RhRegistroResponse>>> ListarAsync(string recurso, RhFiltro filtro, CancellationToken ct);
    Task<Result<RhRegistroResponse>> ObterAsync(string recurso, long id, CancellationToken ct);
    Task<Result<long>> CriarAsync(string recurso, RhRegistroCreateRequest request, CancellationToken ct);
    Task<Result> AtualizarAsync(string recurso, long id, RhRegistroUpdateRequest request, CancellationToken ct);
    Task<Result> ExcluirAsync(string recurso, long id, CancellationToken ct);
    Task<Result<RhDashboardResponse>> DashboardAsync(CancellationToken ct);
    Task<Result<RhPortalResumoResponse>> PortalServidorAsync(long servidorId, CancellationToken ct);
    Task<Result<long>> IntegrarFinanceiroAsync(RhFinanceiroIntegracaoRequest request, CancellationToken ct);
    Task<Result<byte[]>> ExportarAsync(string recurso, string formato, CancellationToken ct);
    Task<Result<long>> ApurarPontoAsync(RhPontoApuracaoRequest request, CancellationToken ct);
    Task<Result<RhPontoAjusteResumoDto>> AjustarPontoRegistroAsync(long registroId, RhPontoRegistrarBatidaRequest request, CancellationToken ct);
    Task<Result<RhJustificativaDecisaoDto>> DecidirJustificativaPontoAsync(long justificativaId, string decisao, CancellationToken ct);
    // RC-EVO-RH §6: homologação/reabertura de apuração
    Task<Result<RhApuracaoHomologacaoResumoDto>> HomologarApuracaoPontoAsync(long apuracaoId, string? observacao, CancellationToken ct);
    Task<Result<RhApuracaoReaberturaResumoDto>> ReabrirApuracaoPontoAsync(long apuracaoId, string justificativa, CancellationToken ct);
    // RC-EVO-RH §7: integra a apuração homologada na folha de destino (lançamentos reais, retry idempotente)
    Task<Result<RhIntegracaoFolhaResumoDto>> IntegrarPontoNaFolhaAsync(long apuracaoId, long folhaId, CancellationToken ct);
    // RC-EVO-RH §8: portal com escopo próprio (contracheques c/ totais recalculados, pendências e seções do próprio servidor)
    Task<Result<PagedResult<RhRegistroResponse>>> PortalSecaoAsync(string secao, RhFiltro filtro, CancellationToken ct);
    Task<Result<PagedResult<RhPortalPendenciaItem>>> PortalPendenciasAsync(CancellationToken ct);
    Task<Result<RhRegistroResponse>> ObterPortalLancamentoAsync(long lancamentoId, CancellationToken ct);
}
