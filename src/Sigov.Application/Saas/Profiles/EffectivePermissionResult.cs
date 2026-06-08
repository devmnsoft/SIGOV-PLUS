namespace Sigov.Application.Saas.Profiles;

public sealed record EffectivePermissionResult(
    long UsuarioId,
    long? TenantId,
    bool Global,
    IReadOnlyCollection<string> ProfileCodes,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<UserAccessScope> Scopes,
    IReadOnlyCollection<string> Restrictions)
{
    public bool HasPermission(string permission) => Global || Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
}
