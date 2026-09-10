using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Sigov.Application.Authorization;
using IAuthorizationEvaluator = Sigov.Application.Authorization.IAuthorizationEvaluator;

namespace Sigov.Infrastructure.Security;

public sealed record PersistedPermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PersistedPermissionHandler(IAuthorizationEvaluator evaluator) : AuthorizationHandler<PersistedPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PersistedPermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || !TryIdentity(context.User, out var userId))
            return;

        var separator = requirement.Permission.LastIndexOf('.');
        var resource = separator > 0 ? requirement.Permission[..separator] : requirement.Permission;
        var action = separator > 0 ? requirement.Permission[(separator + 1)..] : "acessar";
        var moduleSeparator = resource.IndexOf('.');
        var module = moduleSeparator > 0 ? resource[..moduleSeparator] : resource;
        resource = moduleSeparator > 0 ? resource[(moduleSeparator + 1)..] : resource;
        var decision = await evaluator.EvaluateAsync(new AuthorizationRequest(
            userId, module, resource, action,
            PositiveLongClaim(context.User, "tenant_id"),
            PositiveLongClaim(context.User, "entidade_id"),
            PositiveLongClaim(context.User, "exercicio_id"),
            Origem: "POLICY")).ConfigureAwait(false);
        if (decision.Permitido)
            context.Succeed(requirement);
    }

    private static bool TryIdentity(ClaimsPrincipal user, out long userId) => long.TryParse(
        user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("usuario_id") ?? user.FindFirstValue("user_id"), out userId);

    private static long? PositiveLongClaim(ClaimsPrincipal user, string type) =>
        long.TryParse(user.FindFirstValue(type), out var value) && value > 0 ? value : null;
}
