using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Sigov.Application.Security;
using Sigov.Application.Abstractions;

namespace Sigov.Web.Services;

/// <summary>
/// Restores legacy permission claims only in the in-memory request principal.
/// Cookie authentication serializes the compact principal created at login, never this clone.
/// </summary>
public sealed class RequestPermissionClaimsTransformation(
    IAuthenticationRepository authenticationRepository,
    ICurrentTenant currentTenant,
    IHttpContextAccessor httpContextAccessor,
    ILogger<RequestPermissionClaimsTransformation> logger) : IClaimsTransformation
{
    private Task<IReadOnlyCollection<string>>? _permissions;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return principal;

        var transformed = principal.Clone();
        if (transformed.HasClaim(claim => claim.Type == "permission")) return transformed;

        try
        {
            _permissions ??= LoadPermissionsAsync(userId);
            var permissions = await _permissions.ConfigureAwait(false);
            if (transformed.Identity is ClaimsIdentity identity)
                identity.AddClaims(permissions.Select(value => new Claim("permission", value)));
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha fechada ao carregar permissões da requisição. UserId={UserId}; CorrelationId={CorrelationId}",
                userId, httpContextAccessor.HttpContext?.TraceIdentifier);
        }

        return transformed;
    }

    private async Task<IReadOnlyCollection<string>> LoadPermissionsAsync(long userId) =>
        (await authenticationRepository.GetRequestAccessAsync(userId, currentTenant.TenantId, currentTenant.EntidadeId,
            currentTenant.ExercicioId, httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None).ConfigureAwait(false)).Permissions;
}
