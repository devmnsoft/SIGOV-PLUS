using System.Security.Claims;

namespace Sigov.Web.Services;

public interface IUserPermissionService
{
    bool HasPermission(ClaimsPrincipal user, string permission);
}

public sealed class UserPermissionService : IUserPermissionService
{
    public bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;
        if (user.IsInRole("Admin") || user.IsInRole("SUPERADMIN") || user.IsInRole("ADMIN_GERAL") || user.HasClaim("perfil", "admin") || user.HasClaim("perfil", "SUPERADMIN") || user.HasClaim("perfil", "ADMIN_GERAL")) return true;
        return user.HasClaim("permission", permission) || user.HasClaim("permissao", permission) || user.HasClaim("scope", permission);
    }
}

public interface IMenuAuthorizationService
{
    bool CanSee(ClaimsPrincipal user, string menuCode);
}

public sealed class MenuAuthorizationService : IMenuAuthorizationService
{
    private readonly IUserPermissionService _permissions;
    public MenuAuthorizationService(IUserPermissionService permissions) => _permissions = permissions;
    public bool CanSee(ClaimsPrincipal user, string menuCode)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;
        return _permissions.HasPermission(user, menuCode) || _permissions.HasPermission(user, "admin") || menuCode.StartsWith("self:", StringComparison.OrdinalIgnoreCase);
    }
}
