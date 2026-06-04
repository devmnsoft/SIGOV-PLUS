namespace Sigov.Application.Saas;

public interface ITenantContext
{
    long? TenantId { get; }
    string? TenantSlug { get; }
    string? Status { get; }
    bool IsResolved { get; }
    void SetTenant(long tenantId, string slug, string status);
    void Clear();
}

public interface ITenantResolver
{
    Task<TenantResolutionResult> ResolveAsync(string? host, string? headerSlug, string? querySlug, IReadOnlyDictionary<string, string?> claims, bool allowDevelopmentResolvers, CancellationToken cancellationToken);
}

public interface ITenantProvisioningService
{
    Task<ProvisionTenantResult> ProvisionarAsync(ProvisionTenantRequest request, CancellationToken cancellationToken);
}

public interface ITenantAccessGuard
{
    Task<bool> EnsureTenantActiveAsync(long tenantId, CancellationToken cancellationToken);
    Task<bool> EnsureModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken);
    Task<bool> EnsureFeatureAsync(long tenantId, string featureCode, CancellationToken cancellationToken);
}

public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(long tenantId, string featureCode, CancellationToken cancellationToken);
}

public interface IModuloLicenciamentoService
{
    Task<bool> IsModuleEnabledAsync(long tenantId, string moduleCode, CancellationToken cancellationToken);
}

public interface ITenantUsageMeter
{
    Task RegistrarRequisicaoAsync(long tenantId, CancellationToken cancellationToken);
}

public interface ITenantConfigurationProvider
{
    Task<string?> ObterValorJsonAsync(long tenantId, string chave, CancellationToken cancellationToken);
}
