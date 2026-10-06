namespace Sigov.Application.Saas.Modules;

public interface IModuleAccessRepository
{
    Task<TenantModuleContract?> GetTenantModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantModuleContract>> GetTenantModulesAsync(long tenantId, CancellationToken cancellationToken);
    Task<bool> IsFeatureEnabledAsync(long tenantId, string moduleCode, string featureCode, CancellationToken cancellationToken);
    Task UpsertTenantModuleStatusAsync(long tenantId, string moduleCode, string status, long? userId, Guid? correlationId, CancellationToken cancellationToken);
    Task<string?> GetTenantStatusAsync(long tenantId, CancellationToken cancellationToken);
    Task<SaasSubscriptionSnapshot?> GetActiveSubscriptionAsync(long tenantId, CancellationToken cancellationToken);
}

/// <summary>Melhor assinatura comercial do tenant (família B); null = sem assinatura família B (comportamento legado).</summary>
public sealed record SaasSubscriptionSnapshot(string Status, DateOnly? ValidUntil);

public sealed record TenantModuleContract(
    long TenantId,
    string ModuleCode,
    string? PackageCode,
    string Status,
    bool Active,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveUntil = null);
