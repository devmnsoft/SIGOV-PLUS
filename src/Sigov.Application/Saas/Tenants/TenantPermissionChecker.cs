namespace Sigov.Application.Saas.Tenants;

public sealed class TenantPermissionChecker
{
    public bool IsSigovAdmin(IEnumerable<string> roles) => roles.Contains("SIGOV_ADMIN", StringComparer.OrdinalIgnoreCase);
}
