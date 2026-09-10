namespace Sigov.Application.Saas.SuperAdmin;

public sealed record SaasTenantListFilter(string? Search, string? Status, string? Esfera, int Page = 1, int PageSize = 20);

public sealed record SaasTenantListItem(
    long Id,
    string Name,
    string Status,
    string Esfera,
    int Entities,
    int ActiveUsers,
    int Modules,
    DateTimeOffset? LastActivityUtc,
    string? Plan);

public sealed record SaasTenantListPage(IReadOnlyList<SaasTenantListItem> Items, int Page, int PageSize, int Total);

public sealed record SaasTenantEntityItem(long Id, string Name, string? Type, string? Esfera);
public sealed record SaasTenantUserItem(long Id, string Name, string Email, bool Active, IReadOnlyList<string> Profiles);
public sealed record SaasTenantContractItem(
    long Id,
    string ModuleCode,
    string ModuleName,
    string Status,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveUntil,
    string? PlanCode,
    decimal? ContractedValue,
    string? Reason,
    DateTimeOffset? UpdatedAt);

public sealed record SaasTenantDetail(
    long Id,
    string Name,
    string Status,
    string Esfera,
    string? Plan,
    DateTimeOffset? LastActivityUtc,
    IReadOnlyList<SaasTenantEntityItem> Entities,
    IReadOnlyList<SaasTenantUserItem> Users,
    IReadOnlyList<SaasTenantContractItem> Contracts,
    IReadOnlyList<ModuleCatalogOption> Catalog);

public sealed record ModuleCatalogOption(string Code, string Name, IReadOnlyCollection<string> Dependencies);

public sealed record SaasModuleContractCommand(
    long TenantId,
    string ModuleCode,
    string Status,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveUntil,
    string Justification,
    DateTimeOffset? ExpectedUpdatedAt = null);

public sealed record SaasModuleContractResult(bool Success, string Message, SaasTenantContractItem? Contract = null);

public interface ISaasTenantAdministrationService
{
    Task<SaasTenantListPage> ListAsync(SaasTenantListFilter filter, CancellationToken cancellationToken = default);
    Task<SaasTenantDetail?> GetAsync(long tenantId, CancellationToken cancellationToken = default);
    Task<SaasModuleContractResult> ContractAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasModuleContractResult> SuspendAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default);
    Task<SaasModuleContractResult> ReactivateAsync(SaasModuleContractCommand command, long userId, string correlationId, CancellationToken cancellationToken = default);
}
