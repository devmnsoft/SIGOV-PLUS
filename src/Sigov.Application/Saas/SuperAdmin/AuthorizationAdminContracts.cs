namespace Sigov.Application.Saas.SuperAdmin;

public sealed record AuthorizationAdminFilter(string? Search = null, long? TenantId = null, bool IncludeInactive = false);
public sealed record AuthorizationCatalogItem(long Id, string Code, string Name, bool Active);
public sealed record AuthorizationLinkItem(string Kind, long LeftId, string LeftName, long RightId, string RightName,
    long? TenantId, long? EntityId, long? FiscalYearId, long? UnitId, DateTimeOffset? ValidFrom,
    DateTimeOffset? ValidTo, string? Effect, decimal? ApprovalLimit, bool Active, bool Deleted);
public sealed record AuthorizationAdminSnapshot(IReadOnlyList<AuthorizationCatalogItem> Profiles,
    IReadOnlyList<AuthorizationCatalogItem> Groups, IReadOnlyList<AuthorizationCatalogItem> Permissions,
    IReadOnlyList<AuthorizationCatalogItem> Users, IReadOnlyList<AuthorizationLinkItem> UserGroups,
    IReadOnlyList<AuthorizationLinkItem> GroupProfiles, IReadOnlyList<AuthorizationLinkItem> ProfilePermissions);

public sealed record AuthorizationCatalogCommand(long? Id, string Code, string Name, string? Description, bool Active = true);
public sealed record AuthorizationLinkCommand(string Kind, long LeftId, long RightId, long? TenantId,
    long? EntityId, long? FiscalYearId, long? UnitId, DateTimeOffset? ValidFrom, DateTimeOffset? ValidTo,
    string? Effect, decimal? ApprovalLimit, string? Justification, bool Active = true);
public sealed record AuthorizationAdminResult(bool Success, string Message);

public interface IAuthorizationAdminService
{
    Task<AuthorizationAdminSnapshot> ListAsync(AuthorizationAdminFilter filter, CancellationToken cancellationToken);
    Task<AuthorizationAdminResult> SaveCatalogAsync(string kind, AuthorizationCatalogCommand command, long actorUserId, string correlationId, CancellationToken cancellationToken);
    Task<AuthorizationAdminResult> SaveLinkAsync(AuthorizationLinkCommand command, long actorUserId, string correlationId, CancellationToken cancellationToken);
    Task<AuthorizationAdminResult> ChangeStatusAsync(string kind, long leftId, long? rightId, bool active, bool delete, long actorUserId, string correlationId, CancellationToken cancellationToken);
}
