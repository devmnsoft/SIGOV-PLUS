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
        if (user.IsInRole("Admin") || user.HasClaim("perfil", "admin")) return true;
        return user.HasClaim("permission", permission) || user.HasClaim("permissao", permission);
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
