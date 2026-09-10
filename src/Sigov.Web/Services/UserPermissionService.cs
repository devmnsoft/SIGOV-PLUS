using System.Security.Claims;
using Sigov.Application.Authorization;

namespace Sigov.Web.Services;

public interface IUserPermissionService { bool HasPermission(ClaimsPrincipal user, string permission); }

/// <summary>Adapter síncrono para views; o snapshot assíncrono é carregado uma vez por request no middleware.</summary>
public sealed class UserPermissionService(IRequestAuthorizationSnapshot snapshot) : IUserPermissionService
{
    public bool HasPermission(ClaimsPrincipal user, string permission) =>
        user?.Identity?.IsAuthenticated == true && snapshot.Current.HasPermission(permission);
}

public interface IMenuAuthorizationService { bool CanSee(ClaimsPrincipal user, string menuCode); }
public sealed class MenuAuthorizationService : IMenuAuthorizationService
{
    private readonly IUserPermissionService _permissions;
    public MenuAuthorizationService(IUserPermissionService permissions) => _permissions = permissions;
    public bool CanSee(ClaimsPrincipal user, string menuCode) => user?.Identity?.IsAuthenticated == true && _permissions.HasPermission(user, menuCode);
}
