namespace Sigov.Application.Saas.Modules;

public interface IModuleAccessChecker
{
    Task<ModuleAccessResult> CheckModuleAsync(ModuleAccessRequest request, CancellationToken cancellationToken);
    Task<ModuleAccessResult> CheckFeatureAsync(ModuleAccessRequest request, string featureCode, CancellationToken cancellationToken);
}

public sealed record ModuleAccessRequest(long? TenantId, string ModuleCode, IReadOnlyCollection<string> ProfileCodes, bool HasAuditedTenantContext = false);
