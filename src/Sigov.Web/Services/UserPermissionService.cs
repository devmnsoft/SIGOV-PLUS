using System.Security.Claims;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

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
        var moduleSeparator = resource.IndexOf('.');
        var module = moduleSeparator > 0 ? resource[..moduleSeparator] : resource;
        return _evaluator.EvaluateAsync(new AuthorizationRequest(userId, module, resource, action, _tenant.TenantId, _tenant.EntidadeId, _tenant.ExercicioId,
            CorrelationId: null, Origem: "WEB_LEGACY_ADAPTER")).GetAwaiter().GetResult().Permitido;
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

public sealed record PersistedPermissionRequirement(string Permission) : IAuthorizationRequirement;

/// <summary>Handler assíncrono usado pelas policies; claims fornecem somente a identidade.</summary>
public sealed class PersistedPermissionHandler : AuthorizationHandler<PersistedPermissionRequirement>
{
    private readonly IAuthorizationEvaluator _evaluator;
    private readonly ICurrentTenant _tenant;

    public PersistedPermissionHandler(IAuthorizationEvaluator evaluator, ICurrentTenant tenant) =>
        (_evaluator, _tenant) = (evaluator, tenant);

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PersistedPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !TryIdentity(context.User, out var userId)) return;
        var separator = requirement.Permission.LastIndexOf('.');
        var resource = separator > 0 ? requirement.Permission[..separator] : requirement.Permission;
        var action = separator > 0 ? requirement.Permission[(separator + 1)..] : "acessar";
        var moduleSeparator = resource.IndexOf('.');
        var module = moduleSeparator > 0 ? resource[..moduleSeparator] : resource;
        var decision = await _evaluator.EvaluateAsync(new AuthorizationRequest(userId, module, resource, action,
            _tenant.TenantId, _tenant.EntidadeId, _tenant.ExercicioId, Origem: "WEB_POLICY")).ConfigureAwait(false);
        if (decision.Permitido) context.Succeed(requirement);
    }

    private static bool TryIdentity(ClaimsPrincipal user, out long userId) => long.TryParse(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("usuario_id") ?? user.FindFirstValue("user_id"), out userId);
}
