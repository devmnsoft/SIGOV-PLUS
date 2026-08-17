using Sigov.Application.Common;

namespace Sigov.Application.Tributario.TributarioAvancado;

public sealed record TributarioAvancadoContext(long TenantId, long? EntidadeId, long? ExercicioId, long? UsuarioId, string CorrelationId);
public sealed record TributarioRegistroDto(long Id, string? Codigo, string Status, string? Tipo, string? Descricao, decimal? Valor, DateTimeOffset CreatedAt, IReadOnlyDictionary<string, object?> Dados);
public sealed record TributarioOperacaoRequest(string? Codigo, string? Tipo, string Status, string? Descricao, string? Justificativa, decimal? Quantidade, decimal? Valor, long? ReferenciaId, IReadOnlyDictionary<string, object?>? Dados);
public sealed record TributarioDashboardDto(long Total, long Pendentes, long Concluidos, long Alertas, IReadOnlyCollection<TributarioRegistroDto> Recentes);
public sealed record TributarioCarneDashboardDto(long Emitidos, long EmProducao, long Entregues, long Pendentes);
public sealed record TributarioCarneEmissaoDto(long Id, string? Codigo, string Tipo, string Status, decimal QuantidadePrevista);
public sealed record TributarioCarneCriarEmissaoRequest(long ExercicioId, string Tipo, decimal QuantidadePrevista, long LayoutId, string? Descricao);
public sealed record TributarioCarneRegistrarEntregaRequest(string Status, string? Motivo, string? Recebedor);
public sealed record TributarioCarneAtualizarStatusRequest(string Status, string? Justificativa);
public sealed record PortalContribuinteConsultaDto(string CodigoSeguro, string DocumentoMascarado, string Status);
public sealed record PortalContribuinteEmitirGuiaRequest(string CodigoSeguro, long LancamentoId, DateOnly Vencimento);
public sealed record PortalContribuinteEmitirCertidaoRequest(string CodigoSeguro, string Tipo);
public sealed record PortalContribuinteParcelamentoRequest(string CodigoSeguro, long DividaId, int Parcelas);
public sealed record TributarioFiscalizacaoCriarOrdemRequest(long ContribuinteId, long CadastroEconomicoId, long AuditorId, DateOnly PeriodoInicio, DateOnly PeriodoFim, string Descricao);
public sealed record TributarioNfseEmitirNotaRequest(long PrestadorId, long TomadorId, string Servico, decimal Valor, DateOnly Competencia, decimal Aliquota);
public sealed record TributarioNfseCancelarRequest(string Justificativa);
public sealed record TributarioNfseSubstituirRequest(long NotaOrigemId, TributarioNfseEmitirNotaRequest NovaNota);

public interface ITributarioAvancadoRepository
{
    Task<PagedResult<TributarioRegistroDto>> ListarAsync(long tenantId, string recurso, int pagina, int tamanho, CancellationToken ct);
    Task<TributarioRegistroDto?> ObterAsync(long tenantId, string recurso, long id, CancellationToken ct);
    Task<long> CriarAsync(TributarioAvancadoContext contexto, string recurso, TributarioOperacaoRequest request, CancellationToken ct);
    Task<bool> AlterarStatusAsync(TributarioAvancadoContext contexto, string recurso, long id, string status, string? justificativa, CancellationToken ct);
    Task<TributarioDashboardDto> DashboardAsync(long tenantId, string recurso, CancellationToken ct);
}

public interface ITributarioCarnesBoletosRepository : ITributarioAvancadoRepository { }
public interface IPortalContribuinteRepository : ITributarioAvancadoRepository { }
public interface ITributarioFiscalizacaoRepository : ITributarioAvancadoRepository { }
public interface ITributarioNfseRepository : ITributarioAvancadoRepository { }
public interface ITributarioCarnesBoletosService : ITributarioAvancadoRepository { }
public interface ITributarioCarneArquivoService { Task<byte[]> GerarCsvAsync(long tenantId, long emissaoId, CancellationToken ct); }
public interface ITributarioCarneEntregaService : ITributarioCarnesBoletosService { }
public interface ITributarioDamService : ITributarioCarnesBoletosService { }
public interface IPortalContribuinteService : ITributarioAvancadoRepository { }
public interface IPortalContribuinteCertidaoService : IPortalContribuinteService { }
public interface IPortalContribuinteGuiaService : IPortalContribuinteService { }
public interface IPortalContribuinteParcelamentoService : IPortalContribuinteService { }
public interface ITributarioFiscalizacaoService : ITributarioAvancadoRepository { }
public interface ITributarioIssqnService : ITributarioFiscalizacaoService { }
public interface ITributarioSimplesNacionalService : ITributarioFiscalizacaoService { }
public interface ITributarioAutoInfracaoService : ITributarioFiscalizacaoService { }
public interface ITributarioNfseService : ITributarioAvancadoRepository { }
public interface ITributarioLivroEletronicoService : ITributarioNfseService { }
public interface ITributarioDesifService : ITributarioNfseService { }
public interface ITributarioNfseValidacaoService : ITributarioNfseService { }
