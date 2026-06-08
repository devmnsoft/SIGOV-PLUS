namespace Sigov.Application.Saas.Modules;

public interface IModuleAccessRepository
{
    Task<TenantModuleContract?> GetTenantModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantModuleContract>> GetTenantModulesAsync(long tenantId, CancellationToken cancellationToken);
    Task<bool> IsFeatureEnabledAsync(long tenantId, string moduleCode, string featureCode, CancellationToken cancellationToken);
    Task UpsertTenantModuleStatusAsync(long tenantId, string moduleCode, string status, long? userId, Guid? correlationId, CancellationToken cancellationToken);
}

public sealed record TenantModuleContract(long TenantId, string ModuleCode, string? PackageCode, string Status, bool Active);
