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

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
}
