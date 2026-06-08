using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Geo;

public sealed record AgroGeoFiltro(int Page = 1, int PageSize = 20, string? Busca = null);

public sealed record AgroGeoCamadaResponse(long Id, long TenantId, long? EntidadeId, string Codigo, string Nome, string TipoCamada, string? Descricao, bool Publica, bool Ativo);

public sealed record AgroGeoCamadaRequest(string Codigo, string Nome, string TipoCamada, string? Descricao, bool Publica, string? EstiloJson, bool Ativo = true);

public sealed record AgroGeoFeicaoResponse(long Id, long TenantId, long? EntidadeId, long CamadaId, string Nome, string TipoGeometria, decimal? Latitude, decimal? Longitude, string? GeoJson, bool Ativo);

public sealed record AgroGeoFeicaoRequest(long CamadaId, string Nome, string TipoGeometria, decimal? Latitude, decimal? Longitude, string? GeoJson, string? PropriedadesJson, bool Ativo = true);

public interface IAgroGeoRepository
{
    Task<PagedResult<AgroGeoCamadaResponse>> ListarCamadasAsync(long tenantId, long? entidadeId, AgroGeoFiltro filtro, CancellationToken cancellationToken);
    Task<AgroGeoCamadaResponse?> ObterCamadaAsync(long tenantId, long? entidadeId, long id, CancellationToken cancellationToken);
    Task<long> CriarCamadaAsync(long tenantId, long? entidadeId, long? usuarioId, AgroGeoCamadaRequest request, CancellationToken cancellationToken);
    Task AtualizarCamadaAsync(long tenantId, long? entidadeId, long id, long? usuarioId, AgroGeoCamadaRequest request, CancellationToken cancellationToken);
    Task ExcluirCamadaAsync(long tenantId, long? entidadeId, long id, long? usuarioId, CancellationToken cancellationToken);
    Task<PagedResult<AgroGeoFeicaoResponse>> ListarFeicoesAsync(long tenantId, long? entidadeId, AgroGeoFiltro filtro, CancellationToken cancellationToken);
    Task<AgroGeoFeicaoResponse?> ObterFeicaoAsync(long tenantId, long? entidadeId, long id, CancellationToken cancellationToken);
    Task<long> CriarFeicaoAsync(long tenantId, long? entidadeId, long? usuarioId, AgroGeoFeicaoRequest request, CancellationToken cancellationToken);
    Task AtualizarFeicaoAsync(long tenantId, long? entidadeId, long id, long? usuarioId, AgroGeoFeicaoRequest request, CancellationToken cancellationToken);
    Task ExcluirFeicaoAsync(long tenantId, long? entidadeId, long id, long? usuarioId, CancellationToken cancellationToken);
    Task<string> ExportarGeoJsonAsync(long tenantId, long? entidadeId, CancellationToken cancellationToken);
}

public interface IAgroGeoService
{
    Task<Result<PagedResult<AgroGeoCamadaResponse>>> ListarCamadasAsync(AgroGeoFiltro filtro, CancellationToken cancellationToken);
    Task<Result<AgroGeoCamadaResponse>> ObterCamadaAsync(long id, CancellationToken cancellationToken);
    Task<Result<long>> CriarCamadaAsync(AgroGeoCamadaRequest request, CancellationToken cancellationToken);
    Task<Result> AtualizarCamadaAsync(long id, AgroGeoCamadaRequest request, CancellationToken cancellationToken);
    Task<Result> ExcluirCamadaAsync(long id, CancellationToken cancellationToken);
    Task<Result<PagedResult<AgroGeoFeicaoResponse>>> ListarFeicoesAsync(AgroGeoFiltro filtro, CancellationToken cancellationToken);
    Task<Result<AgroGeoFeicaoResponse>> ObterFeicaoAsync(long id, CancellationToken cancellationToken);
    Task<Result<long>> CriarFeicaoAsync(AgroGeoFeicaoRequest request, CancellationToken cancellationToken);
    Task<Result> AtualizarFeicaoAsync(long id, AgroGeoFeicaoRequest request, CancellationToken cancellationToken);
    Task<Result> ExcluirFeicaoAsync(long id, CancellationToken cancellationToken);
    Task<Result<string>> ExportarGeoJsonAsync(CancellationToken cancellationToken);
}
