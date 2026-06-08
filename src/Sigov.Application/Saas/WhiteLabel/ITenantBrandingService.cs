using Sigov.Domain.Common;

namespace Sigov.Application.Saas.WhiteLabel;

public interface ITenantBrandingService
{
    Task<TenantBrandingResponse> GetAsync(long tenantId, CancellationToken cancellationToken);
    Task<Result<TenantBrandingResponse>> UpdateAsync(long tenantId, TenantBrandingUpdateRequest request, long usuarioId, CancellationToken cancellationToken);
}
public interface ITenantBrandingRepository
{
    Task<TenantBrandingResponse?> GetAsync(long tenantId, CancellationToken cancellationToken);
    Task<bool> PlanoPermiteWhiteLabelAsync(long tenantId, CancellationToken cancellationToken);
    Task<TenantBrandingResponse> UpsertAsync(long tenantId, TenantBrandingUpdateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken);
}
