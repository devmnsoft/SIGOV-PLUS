using Sigov.Application.Common;

namespace Sigov.Application.Saneamento.Avancado;

public sealed record SaneamentoAvancadoContext(long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string CorrelationId);
public sealed record SaneamentoAvancadoFiltro(string? Status = null, string? Tipo = null, int Pagina = 1, int Tamanho = 30);
public sealed record SaneamentoAvancadoRegistroDto(long Id, string? Codigo, string? Numero, string? Tipo, string Status, string? Descricao, decimal? Valor, DateTimeOffset CreatedAt);
public sealed record SaneamentoAvancadoOperacaoRequest(long? ConsumidorId, long? LigacaoId, long? HidrometroId, long? UnidadeOperacionalId, long? OrdemServicoId, long? ReferenciaId, string? Codigo, string? Numero, string? Tipo, string Status, string? Descricao, DateOnly? DataReferencia, DateOnly? Competencia, decimal? Latitude, decimal? Longitude, decimal? Quantidade, decimal? Valor, string? Justificativa, IReadOnlyDictionary<string, object?>? Dados);
public sealed record SaneamentoAvancadoDashboardDto(long Total, long Ativos, long Pendentes, long Alertas, decimal Valor, IReadOnlyCollection<SaneamentoAvancadoRegistroDto> Recentes);

public interface ISaneamentoAvancadoRepository
{
    Task<PagedResult<SaneamentoAvancadoRegistroDto>> ListarAsync(long tenantId, string recurso, SaneamentoAvancadoFiltro filtro, CancellationToken ct);
    Task<SaneamentoAvancadoRegistroDto?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct);
    Task<long> CriarAsync(SaneamentoAvancadoContext contexto, string recurso, SaneamentoAvancadoOperacaoRequest request, CancellationToken ct);
    Task<bool> AlterarStatusAsync(SaneamentoAvancadoContext contexto, string recurso, long id, string status, string? justificativa, CancellationToken ct);
    Task<SaneamentoAvancadoDashboardDto> DashboardAsync(long tenantId, string recurso, CancellationToken ct);
    Task<byte[]> ExportarCsvAsync(long tenantId, string recurso, CancellationToken ct);
}
public interface ISaneamentoComercialRepository : ISaneamentoAvancadoRepository { }
public interface ISaneamentoComercialService : ISaneamentoAvancadoRepository { }
public interface ISaneamentoFaturamentoRepository : ISaneamentoAvancadoRepository { }
public interface ISaneamentoFaturamentoService : ISaneamentoAvancadoRepository { }
public interface ISaneamentoOperacaoRepository : ISaneamentoAvancadoRepository { }
public interface ISaneamentoOperacaoService : ISaneamentoAvancadoRepository { }
public interface ISaneamentoGisQualidadeRepository : ISaneamentoAvancadoRepository { }
public interface ISaneamentoGisQualidadeService : ISaneamentoAvancadoRepository { }
public interface ISaneamentoLigacaoService : ISaneamentoComercialService { }
public interface ISaneamentoHidrometroService : ISaneamentoComercialService { }
public interface ISaneamentoAtendimentoService : ISaneamentoComercialService { }
public interface ISaneamentoLeituraService : ISaneamentoFaturamentoService { }
public interface ISaneamentoFaturaService : ISaneamentoFaturamentoService { }
public interface ISaneamentoArrecadacaoService : ISaneamentoFaturamentoService { }
public interface ISaneamentoInadimplenciaService : ISaneamentoFaturamentoService { }
public interface ISaneamentoOrdemServicoService : ISaneamentoOperacaoService { }
public interface ISaneamentoCorteReligacaoService : ISaneamentoOperacaoService { }
public interface ISaneamentoVazamentoService : ISaneamentoOperacaoService { }
public interface ISaneamentoEquipeService : ISaneamentoOperacaoService { }
public interface ISaneamentoGisService : ISaneamentoGisQualidadeService { }
public interface ISaneamentoLaboratorioService : ISaneamentoGisQualidadeService { }
public interface ISaneamentoQualidadeService : ISaneamentoGisQualidadeService { }
