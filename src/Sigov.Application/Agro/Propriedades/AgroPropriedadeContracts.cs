using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Propriedades;

public sealed record AgroPropriedadeFiltro(int Page = 1, int PageSize = 20, string? Busca = null, long? ProdutorId = null);
public sealed record AgroPropriedadeCreateRequest(long ProdutorId, string? CodigoPropriedade, string Nome, string? Localidade, string? Comunidade, string? EnderecoJson, decimal? AreaTotalHa, decimal? AreaProdutivaHa, decimal? AreaPreservacaoHa, decimal? Latitude, decimal? Longitude, string? GeoJson, string Situacao, string? Observacao);
public sealed record AgroPropriedadeUpdateRequest(long ProdutorId, string CodigoPropriedade, string Nome, string? Localidade, string? Comunidade, string? EnderecoJson, decimal? AreaTotalHa, decimal? AreaProdutivaHa, decimal? AreaPreservacaoHa, decimal? Latitude, decimal? Longitude, string? GeoJson, string Situacao, string? Observacao, bool Ativo = true);
public sealed record AgroPropriedadeResponse(long Id, long TenantId, long EntidadeId, long ProdutorId, string CodigoPropriedade, string Nome, string? Localidade, string? Comunidade, decimal? AreaTotalHa, decimal? AreaProdutivaHa, string Situacao, bool Ativo);
public sealed record AgroPropriedadeDetalheResponse(long Id, long TenantId, long EntidadeId, long ProdutorId, string CodigoPropriedade, string Nome, string? Localidade, string? Comunidade, string? EnderecoJson, decimal? AreaTotalHa, decimal? AreaProdutivaHa, decimal? AreaPreservacaoHa, decimal? Latitude, decimal? Longitude, string? GeoJson, string Situacao, string? Observacao, bool Ativo);
public sealed record AgroTalhaoCreateRequest(long PropriedadeId, string Codigo, string Nome, decimal AreaHa, string? TipoSolo, bool Irrigado, decimal? Latitude, decimal? Longitude, string? GeoJson, string Situacao);
public sealed record AgroTalhaoResponse(long Id, long TenantId, long EntidadeId, long PropriedadeId, string Codigo, string Nome, decimal AreaHa, string? TipoSolo, bool Irrigado, decimal? Latitude, decimal? Longitude, string? GeoJson, string Situacao, bool Ativo);
public sealed record AgroCulturaCreateRequest(string Codigo, string Nome, string TipoCultura, int? CicloDias, string UnidadeMedida);
public sealed record AgroCulturaResponse(long Id, long TenantId, long EntidadeId, string Codigo, string Nome, string TipoCultura, int? CicloDias, string UnidadeMedida, bool Ativo);
public sealed record AgroSafraCreateRequest(long? ExercicioId, string Codigo, string Nome, int AnoInicio, int AnoFim, DateOnly? DataInicio, DateOnly? DataFim, string Status);
public sealed record AgroSafraResponse(long Id, long TenantId, long EntidadeId, long? ExercicioId, string Codigo, string Nome, int AnoInicio, int AnoFim, DateOnly? DataInicio, DateOnly? DataFim, string Status, bool Ativo);

public interface IAgroPropriedadeRepository
{
    Task<PagedResult<AgroPropriedadeResponse>> ListarAsync(long tenantId, long entidadeId, AgroPropriedadeFiltro filtro, CancellationToken cancellationToken);
    Task<AgroPropriedadeDetalheResponse?> ObterAsync(long tenantId, long entidadeId, long id, CancellationToken cancellationToken);
    Task<long> CriarAsync(long tenantId, long entidadeId, long? usuarioId, AgroPropriedadeCreateRequest request, string codigo, CancellationToken cancellationToken);
    Task AtualizarAsync(long tenantId, long entidadeId, long id, long? usuarioId, AgroPropriedadeUpdateRequest request, CancellationToken cancellationToken);
    Task ExcluirAsync(long tenantId, long entidadeId, long id, long? usuarioId, CancellationToken cancellationToken);
    Task<PagedResult<AgroTalhaoResponse>> ListarTalhoesAsync(long tenantId, long entidadeId, int page, int pageSize, long? propriedadeId, CancellationToken cancellationToken);
    Task<long> CriarTalhaoAsync(long tenantId, long entidadeId, long? usuarioId, AgroTalhaoCreateRequest request, CancellationToken cancellationToken);
    Task<PagedResult<AgroCulturaResponse>> ListarCulturasAsync(long tenantId, long entidadeId, int page, int pageSize, CancellationToken cancellationToken);
    Task<long> CriarCulturaAsync(long tenantId, long entidadeId, long? usuarioId, AgroCulturaCreateRequest request, CancellationToken cancellationToken);
    Task<PagedResult<AgroSafraResponse>> ListarSafrasAsync(long tenantId, long entidadeId, long? exercicioId, int page, int pageSize, CancellationToken cancellationToken);
    Task<long> CriarSafraAsync(long tenantId, long entidadeId, long? exercicioId, long? usuarioId, AgroSafraCreateRequest request, CancellationToken cancellationToken);
}

public interface IAgroPropriedadeService { Task<Result<PagedResult<AgroPropriedadeResponse>>> ListarAsync(AgroPropriedadeFiltro filtro, CancellationToken cancellationToken); Task<Result<AgroPropriedadeDetalheResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(AgroPropriedadeCreateRequest request, CancellationToken cancellationToken); Task<Result> AtualizarAsync(long id, AgroPropriedadeUpdateRequest request, CancellationToken cancellationToken); Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken); }
public interface IAgroTalhaoService { Task<Result<PagedResult<AgroTalhaoResponse>>> ListarAsync(int page, int pageSize, long? propriedadeId, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(long propriedadeId, AgroTalhaoCreateRequest request, CancellationToken cancellationToken); }
public interface IAgroCulturaService { Task<Result<PagedResult<AgroCulturaResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(AgroCulturaCreateRequest request, CancellationToken cancellationToken); }
public interface IAgroSafraService { Task<Result<PagedResult<AgroSafraResponse>>> ListarAsync(int page, int pageSize, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(AgroSafraCreateRequest request, CancellationToken cancellationToken); }
