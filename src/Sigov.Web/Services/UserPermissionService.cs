using System.Security.Claims;
using Sigov.Application.Authorization;
using Microsoft.AspNetCore.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Web.Services;

public interface IUserPermissionService { bool HasPermission(ClaimsPrincipal user, string permission); }

/// <summary>Adapter síncrono legado para views; usa o snapshot server-side carregado uma vez na requisição.</summary>
public sealed class UserPermissionService : IUserPermissionService
{
    public bool HasPermission(ClaimsPrincipal user, string permission)
    {
        return user?.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(permission) &&
               user.HasClaim(claim => claim.Type == "permission" &&
                   string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
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

    public PersistedPermissionHandler(IAuthorizationEvaluator evaluator) => _evaluator = evaluator;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PersistedPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !TryIdentity(context.User, out var userId)) return;
        var separator = requirement.Permission.LastIndexOf('.');
        var resource = separator > 0 ? requirement.Permission[..separator] : requirement.Permission;
        var action = separator > 0 ? requirement.Permission[(separator + 1)..] : "acessar";
        var moduleSeparator = resource.IndexOf('.');
        var module = moduleSeparator > 0 ? resource[..moduleSeparator] : resource;
        resource = moduleSeparator > 0 ? resource[(moduleSeparator + 1)..] : resource;
        var tenantId = PositiveLongClaim(context.User, "tenant_id");
        var decision = await _evaluator.EvaluateAsync(new AuthorizationRequest(userId, module, resource, action,
            tenantId, PositiveLongClaim(context.User, "entidade_id"), PositiveLongClaim(context.User, "exercicio_id"),
            Origem: "WEB_POLICY")).ConfigureAwait(false);
        if (decision.Permitido) context.Succeed(requirement);
    }

    private static bool TryIdentity(ClaimsPrincipal user, out long userId) => long.TryParse(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("usuario_id") ?? user.FindFirstValue("user_id"), out userId);

    private static long? PositiveLongClaim(ClaimsPrincipal user, string type) =>
        long.TryParse(user.FindFirstValue(type), out var value) && value > 0 ? value : null;
}
