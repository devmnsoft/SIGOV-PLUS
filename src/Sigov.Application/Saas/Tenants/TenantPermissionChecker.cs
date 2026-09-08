namespace Sigov.Application.Saas.Tenants;

using Sigov.Domain.Saas;

public sealed class TenantPermissionChecker
{
    public bool IsSigovAdmin(IEnumerable<string> roles) => roles.Any(PerfilNivelCodigos.GlobalAdminAliases.Contains);
}
