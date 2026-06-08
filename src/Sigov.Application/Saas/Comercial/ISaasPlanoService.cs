using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public interface ISaasPlanoService
{
    Task<IReadOnlyCollection<SaasPlanoResponse>> ListPublicAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SaasPlanoResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<SaasPlanoDetalheResponse?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken);
    Task<Result<SaasPlanoResponse>> CreateAsync(SaasPlanoCreateRequest request, long usuarioId, CancellationToken cancellationToken);
    Task<Result<SaasPlanoResponse>> UpdateAsync(long id, SaasPlanoUpdateRequest request, long usuarioId, CancellationToken cancellationToken);
}
public interface ISaasPlanoRepository
{
    Task<IReadOnlyCollection<SaasPlanoResponse>> ListPublicAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SaasPlanoResponse>> ListAdminAsync(int offset, int limit, CancellationToken cancellationToken);
    Task<SaasPlanoDetalheResponse?> GetByCodigoAsync(string codigo, CancellationToken cancellationToken);
    Task<SaasPlanoResponse> CreateAsync(SaasPlanoCreateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task<SaasPlanoResponse> UpdateAsync(long id, SaasPlanoUpdateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken);
}
