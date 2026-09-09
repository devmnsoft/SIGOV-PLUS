using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Sigov.Application.Security;
using Sigov.Application.Saas.Modules;

namespace Sigov.Web.Services;

/// <summary>
/// Restores legacy permission claims only in the in-memory request principal.
/// Cookie authentication serializes the compact principal created at login, never this clone.
/// </summary>
public sealed class RequestPermissionClaimsTransformation(
    IHttpContextAccessor httpContextAccessor,
    ILogger<RequestPermissionClaimsTransformation> logger) : IClaimsTransformation
{
    private Task<AuthenticationAccess>? _access;
    private Task<IReadOnlyCollection<TenantModuleContract>>? _modules;

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true ||
            !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId <= 0)
            return principal;

        var transformed = principal.Clone();
        if (transformed.HasClaim(claim => claim.Type == "permission")) return transformed;

        try
        {
            var requestServices = httpContextAccessor.HttpContext?.RequestServices
                ?? throw new InvalidOperationException("RequestServices indisponível para carregar permissões.");
            var authenticationRepository = requestServices.GetRequiredService<IAuthenticationRepository>();
            var moduleAccessRepository = requestServices.GetRequiredService<IModuleAccessRepository>();
            var tenantId = PositiveLongClaim(principal, "tenant_id");
            var entidadeId = PositiveLongClaim(principal, "entidade_id");
            var exercicioId = PositiveLongClaim(principal, "exercicio_id");
            _access ??= authenticationRepository.GetRequestAccessAsync(userId, tenantId, entidadeId, exercicioId,
                httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
            var access = await _access.ConfigureAwait(false);
            if (transformed.Identity is ClaimsIdentity identity)
            {
                identity.AddClaims(access.Permissions.Select(value => new Claim("permission", value)));
                if (tenantId.HasValue)
                {
                    _modules ??= moduleAccessRepository.GetTenantModulesAsync(tenantId.Value,
                        httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
                    var today = DateOnly.FromDateTime(DateTime.UtcNow);
                    var modules = await _modules.ConfigureAwait(false);
                    identity.AddClaims(modules
                        .Where(module => module.Active && IsEnabled(module.Status) &&
                            (!module.EffectiveFrom.HasValue || module.EffectiveFrom <= today) &&
                            (!module.EffectiveUntil.HasValue || module.EffectiveUntil >= today))
                        .Select(module => new Claim("module", module.ModuleCode)));
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Falha fechada ao carregar permissões da requisição. UserId={UserId}; CorrelationId={CorrelationId}",
                userId, httpContextAccessor.HttpContext?.TraceIdentifier);
        }

        return transformed;
    }

    private static long? PositiveLongClaim(ClaimsPrincipal principal, string type) =>
        long.TryParse(principal.FindFirstValue(type), out var value) && value > 0 ? value : null;

    private static bool IsEnabled(string status) => status.Equals("CONTRATADO", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("HABILITADO", StringComparison.OrdinalIgnoreCase) || status.Equals("ATIVO", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("TRIAL", StringComparison.OrdinalIgnoreCase) || status.Equals("EM_IMPLANTACAO", StringComparison.OrdinalIgnoreCase) ||
        status.Equals("BETA", StringComparison.OrdinalIgnoreCase);
}
