using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Application.Agro.Producao;

public sealed record AgroProducaoAgricolaFiltro(int Page = 1, int PageSize = 20, long? ProdutorId = null, long? CulturaId = null, long? SafraId = null);
public sealed record AgroProducaoAgricolaCreateRequest(long ProdutorId, long? PropriedadeId, long? TalhaoId, long CulturaId, long? SafraId, decimal AreaPlantadaHa, DateOnly? DataPlantio, DateOnly? DataColheitaPrevista, DateOnly? DataColheitaReal, decimal? ProducaoEstimada, decimal? ProducaoRealizada, string UnidadeMedida, string Status, string? Observacao);
public sealed record AgroProducaoAgricolaResponse(long Id, long TenantId, long EntidadeId, long? ExercicioId, long ProdutorId, long? PropriedadeId, long? TalhaoId, long CulturaId, long? SafraId, decimal AreaPlantadaHa, DateOnly? DataPlantio, DateOnly? DataColheitaPrevista, DateOnly? DataColheitaReal, decimal? ProducaoEstimada, decimal? ProducaoRealizada, string UnidadeMedida, decimal? Produtividade, string Status, string? Observacao, bool Ativo);

public interface IAgroProducaoRepository
{
    Task<PagedResult<AgroProducaoAgricolaResponse>> ListarAsync(long tenantId, long entidadeId, long? exercicioId, AgroProducaoAgricolaFiltro filtro, CancellationToken cancellationToken);
    Task<AgroProducaoAgricolaResponse?> ObterAsync(long tenantId, long entidadeId, long id, CancellationToken cancellationToken);
    Task<long> CriarAsync(long tenantId, long entidadeId, long? exercicioId, long? usuarioId, AgroProducaoAgricolaCreateRequest request, decimal? produtividade, CancellationToken cancellationToken);
    Task AtualizarAsync(long tenantId, long entidadeId, long id, long? exercicioId, long? usuarioId, AgroProducaoAgricolaCreateRequest request, decimal? produtividade, CancellationToken cancellationToken);
    Task ExcluirAsync(long tenantId, long entidadeId, long id, long? usuarioId, CancellationToken cancellationToken);
}
public interface IAgroProducaoAgricolaService { Task<Result<PagedResult<AgroProducaoAgricolaResponse>>> ListarAsync(AgroProducaoAgricolaFiltro filtro, CancellationToken cancellationToken); Task<Result<AgroProducaoAgricolaResponse>> ObterAsync(long id, CancellationToken cancellationToken); Task<Result<long>> CriarAsync(AgroProducaoAgricolaCreateRequest request, CancellationToken cancellationToken); Task<Result> AtualizarAsync(long id, AgroProducaoAgricolaCreateRequest request, CancellationToken cancellationToken); Task<Result> ExcluirAsync(long id, CancellationToken cancellationToken); }
