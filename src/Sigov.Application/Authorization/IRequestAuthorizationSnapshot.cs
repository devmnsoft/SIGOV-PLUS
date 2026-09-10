namespace Sigov.Application.Authorization;

public interface IRequestAuthorizationSnapshot
{
    RequestAuthorizationState Current { get; }
    Task<RequestAuthorizationState> GetAsync(CancellationToken cancellationToken = default);
}

public sealed record RequestAuthorizationState(
    bool Authenticated,
    long UserId,
    long? TenantId,
    long? EntidadeId,
    long? ExercicioId,
    long AuthVersion,
    IReadOnlySet<string> Roles,
    IReadOnlySet<string> Permissions,
    IReadOnlySet<string> EntitledModules)
{
    public static RequestAuthorizationState Anonymous { get; } = new(
        false, 0, null, null, null, 0,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        new HashSet<string>(StringComparer.OrdinalIgnoreCase));

    public bool HasPermission(string permission) =>
        Authenticated && !string.IsNullOrWhiteSpace(permission) && Permissions.Contains(permission);

    public bool HasModule(string moduleCode) =>
        Authenticated && !string.IsNullOrWhiteSpace(moduleCode) && EntitledModules.Contains(moduleCode);
}
