using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sigov.Application.Abstractions;

namespace Sigov.Infrastructure.Security;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public long? UsuarioId
    {
        get
        {
            var principal = Principal;
            if (principal is null)
                return null;

            foreach (var claimType in new[] { ClaimTypes.NameIdentifier, "sub", "usuario_id" })
            {
                var value = principal.FindFirst(claimType)?.Value;
                if (long.TryParse(value, out var id) && id > 0)
                    return id;
            }

            return null;
        }
    }

    public long? UserId => UsuarioId;

    public long? TenantId => PositiveLongClaim("tenant_id");

    public string? Nome
    {
        get
        {
            var principal = Principal;
            if (principal is null)
                return null;

            foreach (var claimType in new[] { ClaimTypes.Name, "name", "login" })
            {
                var value = principal.FindFirst(claimType)?.Value;
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }

    public string? Login => ClaimValue("login");

    public string? Email => ClaimValue(ClaimTypes.Email);

    public string? TenantName => ClaimValue("tenant_name");

    public IReadOnlyCollection<string> Roles => ClaimValues(ClaimTypes.Role);

    public IReadOnlyCollection<string> Permissions => ClaimValues("permission");

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    private long? PositiveLongClaim(string claimType)
    {
        var value = ClaimValue(claimType);
        return long.TryParse(value, out var id) && id > 0 ? id : null;
    }

    private string? ClaimValue(string claimType)
    {
        var value = Principal?.FindFirst(claimType)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private IReadOnlyCollection<string> ClaimValues(string claimType) => Principal?
        .FindAll(claimType)
        .Select(claim => claim.Value)
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<string>();
}
