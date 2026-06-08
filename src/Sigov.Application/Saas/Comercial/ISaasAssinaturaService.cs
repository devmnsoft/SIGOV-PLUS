using Sigov.Domain.Common;

namespace Sigov.Application.Saas.Comercial;

public interface ISaasAssinaturaService
{
    Task<IReadOnlyCollection<SaasAssinaturaResponse>> ListAdminAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<SaasAssinaturaResponse?> GetAdminAsync(long id, CancellationToken cancellationToken);
    Task<SaasAssinaturaResponse?> GetMinhaAssinaturaAsync(long tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetMeusModulosAsync(long tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SaasPlanoLimiteResponse>> GetMeusLimitesAsync(long tenantId, CancellationToken cancellationToken);
    Task<Result<SaasAssinaturaResponse>> UpdateAsync(long id, SaasAssinaturaUpdateRequest request, long usuarioId, CancellationToken cancellationToken);
    Task<Result> ChangeStatusAsync(long id, string status, long usuarioId, CancellationToken cancellationToken);
}
public interface ISaasAssinaturaRepository
{
    Task<IReadOnlyCollection<SaasAssinaturaResponse>> ListAdminAsync(int offset, int limit, CancellationToken cancellationToken);
    Task<SaasAssinaturaResponse?> GetAdminAsync(long id, CancellationToken cancellationToken);
    Task<SaasAssinaturaResponse?> GetByTenantAsync(long tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetModulesByTenantAsync(long tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<SaasPlanoLimiteResponse>> GetLimitsByTenantAsync(long tenantId, CancellationToken cancellationToken);
    Task<SaasAssinaturaResponse> UpdateAsync(long id, SaasAssinaturaUpdateRequest request, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task ChangeStatusAsync(long id, string status, long usuarioId, Guid correlationId, CancellationToken cancellationToken);
    Task InsertEventoAsync(long? tenantId, string tipoEvento, string origem, long? origemId, object payload, Guid correlationId, CancellationToken cancellationToken);
}
