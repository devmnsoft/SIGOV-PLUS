using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Sigov.Web.Services;

/// <summary>
/// Mantido somente como no-op fail-closed. Permissões e módulos não voltam ao principal;
/// a decisão usa IRequestAuthorizationSnapshot carregado uma vez por request.
/// </summary>
public sealed class RequestPermissionClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
}
