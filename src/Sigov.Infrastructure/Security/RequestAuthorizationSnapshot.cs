using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sigov.Application.Authorization;
using Sigov.Application.Saas.Modules;
using Sigov.Application.Security;
using Sigov.Domain.Saas;

namespace Sigov.Infrastructure.Security;

public sealed class RequestAuthorizationSnapshot(
    IHttpContextAccessor httpContextAccessor,
    IAuthenticationRepository authenticationRepository,
    IModuleAccessRepository moduleAccessRepository,
    IModuleAccessChecker moduleAccessChecker) : IRequestAuthorizationSnapshot
{
    private Task<RequestAuthorizationState>? _load;
    private RequestAuthorizationState _current = RequestAuthorizationState.Anonymous;

    public RequestAuthorizationState Current => _current;

    public Task<RequestAuthorizationState> GetAsync(CancellationToken cancellationToken = default) =>
        _load ??= LoadAsync(cancellationToken);

    private async Task<RequestAuthorizationState> LoadAsync(CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true || !TryUserId(principal, out var userId))
        {
            _current = RequestAuthorizationState.Anonymous;
            return _current;
        }

        var tenantId = PositiveLong(principal, "tenant_id");
        var entidadeId = PositiveLong(principal, "entidade_id");
        var exercicioId = PositiveLong(principal, "exercicio_id");
        var authVersion = long.TryParse(principal.FindFirstValue("auth_version"), out var version) ? version : 0;
        var roles = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var access = await authenticationRepository.GetRequestAccessAsync(userId, tenantId, entidadeId, exercicioId, cancellationToken)
            .ConfigureAwait(false);
        foreach (var role in access.Roles)
            roles.Add(role);

        var permissions = access.Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entitled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (tenantId.HasValue)
        {
            var profiles = roles.Count > 0 ? (IReadOnlyCollection<string>)roles : new[] { PerfilNivelCodigos.Operador };
            var hasAuditedContext = principal.HasClaim("contexto_auditado", "true");
            var contracts = await moduleAccessRepository.GetTenantModulesAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
            foreach (var contract in contracts)
            {
                var decision = await moduleAccessChecker.CheckModuleAsync(
                    new ModuleAccessRequest(tenantId, contract.ModuleCode, profiles, hasAuditedContext), cancellationToken).ConfigureAwait(false);
                if (decision.Allowed)
                    entitled.Add(contract.ModuleCode);
            }
        }

        _current = new RequestAuthorizationState(true, userId, tenantId, entidadeId, exercicioId, authVersion, roles, permissions, entitled);
        return _current;
    }

    private static bool TryUserId(ClaimsPrincipal principal, out long userId) =>
        long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("usuario_id"), out userId) && userId > 0;

    private static long? PositiveLong(ClaimsPrincipal principal, string type) =>
        long.TryParse(principal.FindFirstValue(type), out var value) && value > 0 ? value : null;
}
