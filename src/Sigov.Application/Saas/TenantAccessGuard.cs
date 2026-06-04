namespace Sigov.Application.Saas;

public sealed class TenantAccessGuard : ITenantAccessGuard
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase) { "ATIVO", "IMPLANTACAO", "HOMOLOGACAO" };
    private readonly Func<long, CancellationToken, Task<string?>> _tenantStatusProvider;
    private readonly IModuloLicenciamentoService _moduloLicenciamentoService;
    private readonly IFeatureFlagService _featureFlagService;

    public TenantAccessGuard(Func<long, CancellationToken, Task<string?>> tenantStatusProvider, IModuloLicenciamentoService moduloLicenciamentoService, IFeatureFlagService featureFlagService)
    {
        _tenantStatusProvider = tenantStatusProvider;
        _moduloLicenciamentoService = moduloLicenciamentoService;
        _featureFlagService = featureFlagService;
    }

    public async Task<bool> EnsureTenantActiveAsync(long tenantId, CancellationToken cancellationToken)
    {
        var status = await _tenantStatusProvider(tenantId, cancellationToken).ConfigureAwait(false);
        return status is not null && AllowedStatuses.Contains(status);
    }

    public Task<bool> EnsureModuleAsync(long tenantId, string moduleCode, CancellationToken cancellationToken) =>
        _moduloLicenciamentoService.IsModuleEnabledAsync(tenantId, moduleCode, cancellationToken);

    public Task<bool> EnsureFeatureAsync(long tenantId, string featureCode, CancellationToken cancellationToken) =>
        _featureFlagService.IsEnabledAsync(tenantId, featureCode, cancellationToken);
}
