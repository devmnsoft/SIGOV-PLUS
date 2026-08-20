using System.Security.Claims;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;

namespace Sigov.Web.Services;

public interface IUserPermissionService { bool HasPermission(ClaimsPrincipal user, string permission); }

/// <summary>Adapter síncrono legado para views; não interpreta roles nem claims de permissão.</summary>
public sealed class UserPermissionService : IUserPermissionService
{
    private readonly IAuthorizationEvaluator _evaluator;
    private readonly ICurrentTenant _tenant;
    public UserPermissionService(IAuthorizationEvaluator evaluator, ICurrentTenant tenant) => (_evaluator, _tenant) = (evaluator, tenant);

    public bool HasPermission(ClaimsPrincipal user, string permission)
    {
        if (user?.Identity?.IsAuthenticated != true || !TryUserId(user, out var userId) || string.IsNullOrWhiteSpace(permission)) return false;
        var separator = permission.LastIndexOf('.');
        var resource = separator > 0 ? permission[..separator] : permission;
        var action = separator > 0 ? permission[(separator + 1)..] : "acessar";
        return _evaluator.EvaluateAsync(new AuthorizationRequest(userId, resource, action, _tenant.TenantId, _tenant.EntidadeId, _tenant.ExercicioId)).GetAwaiter().GetResult().Permitido;
    }

    private static bool TryUserId(ClaimsPrincipal user, out long id)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("usuario_id") ?? user.FindFirstValue("user_id");
        return long.TryParse(value, out id);
    }
}

public interface IMenuAuthorizationService { bool CanSee(ClaimsPrincipal user, string menuCode); }
public sealed class MenuAuthorizationService : IMenuAuthorizationService
{
    private readonly IUserPermissionService _permissions;
    public MenuAuthorizationService(IUserPermissionService permissions) => _permissions = permissions;
    public bool CanSee(ClaimsPrincipal user, string menuCode) => user?.Identity?.IsAuthenticated == true && _permissions.HasPermission(user, menuCode);
}
