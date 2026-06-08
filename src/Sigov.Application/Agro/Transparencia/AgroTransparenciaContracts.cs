using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Transparencia;

public sealed record AgroDatasetPublicoCreateRequest(string Codigo, string Nome, string TipoDataset, string? Descricao = null, string FormatoPadrao = "CSV", bool Anonimizado = true, bool Publico = false, long? EntidadeId = null);
public sealed record PublicarAgroDatasetRequest(string Formato = "CSV");
public sealed record AgroDatasetPublicoResponse(long Id, long TenantId, long? EntidadeId, string Codigo, string Nome, string TipoDataset, string FormatoPadrao, bool Anonimizado, bool Publico, bool Ativo, DateTime? UltimaPublicacaoAt);
public sealed record AgroDatasetPublicacaoResponse(long Id, long TenantId, long DatasetId, string Status, string Formato, string? ConteudoTexto, long? TotalRegistros, DateTime? PublicadoAt);
public interface IAgroTransparenciaRepository
{
    Task<IReadOnlyCollection<AgroDatasetPublicoResponse>> ListarDatasetsAsync(long tenantId, long? entidadeId, bool somentePublicos, int page, int pageSize, CancellationToken cancellationToken);
    Task<AgroDatasetPublicoResponse> CriarDatasetAsync(long tenantId, long? entidadeId, long usuarioId, AgroDatasetPublicoCreateRequest request, CancellationToken cancellationToken);
    Task<AgroDatasetPublicacaoResponse> PublicarAsync(long tenantId, long datasetId, long usuarioId, PublicarAgroDatasetRequest request, CancellationToken cancellationToken);
    Task<AgroDatasetPublicacaoResponse> SuspenderAsync(long tenantId, long datasetId, long usuarioId, CancellationToken cancellationToken);
    Task<long?> ResolverTenantPorSlugAsync(string tenantSlug, CancellationToken cancellationToken);
    Task<AgroDatasetPublicacaoResponse?> ObterPublicacaoAsync(long tenantId, string codigo, string formato, CancellationToken cancellationToken);
    Task RegistrarDownloadAsync(long tenantId, long? datasetId, long? publicacaoId, string formato, string? ip, string? userAgent, CancellationToken cancellationToken);
}
public interface IAgroTransparenciaService
{
    Task<Result<IReadOnlyCollection<AgroDatasetPublicoResponse>>> ListarDatasetsAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<Result<AgroDatasetPublicoResponse>> CriarDatasetAsync(AgroDatasetPublicoCreateRequest request, CancellationToken cancellationToken);
    Task<Result<AgroDatasetPublicacaoResponse>> PublicarAsync(long datasetId, PublicarAgroDatasetRequest request, CancellationToken cancellationToken);
    Task<Result<AgroDatasetPublicacaoResponse>> SuspenderAsync(long datasetId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<AgroDatasetPublicoResponse>>> ListarPublicosAsync(string tenantSlug, CancellationToken cancellationToken);
    Task<Result<AgroDatasetPublicacaoResponse>> DownloadPublicoAsync(string tenantSlug, string codigo, string formato, string? ip, string? userAgent, CancellationToken cancellationToken);
}
