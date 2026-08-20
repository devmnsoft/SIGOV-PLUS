using System.Security.Claims;
using Sigov.Application.Saas;

namespace Sigov.Api.Middlewares;

public sealed class TenantResolutionMiddleware
{
    private static readonly PathString[] PublicPrefixes =
    {
        new("/api/health"),
        new("/api/saas/contexto"),
        new("/api/saas/admin"),
        new("/api/operacao/backups"),
        new("/api/operacao/restores"),
        new("/swagger")
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, ITenantResolver resolver, ITenantContext tenantContext, ITenantUsageMeter usageMeter)
    {
        if (PublicPrefixes.Any(prefix => httpContext.Request.Path.StartsWithSegments(prefix)))
        {
            await _next(httpContext).ConfigureAwait(false);
            return;
        }

        const bool allowDevelopmentResolvers = false;
        var claims = httpContext.User.Claims
            .GroupBy(static claim => claim.Type, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => (string?)group.First().Value, StringComparer.OrdinalIgnoreCase);
        if (httpContext.User.FindFirstValue("tenant_id") is { Length: > 0 } tenantIdClaim)
        {
            claims["tenant_id"] = tenantIdClaim;
        }

        var result = await resolver.ResolveAsync(
            httpContext.Request.Host.Value,
            null,
            null,
            claims,
            allowDevelopmentResolvers,
            httpContext.RequestAborted).ConfigureAwait(false);

        if (!result.Resolved || result.Tenant is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new { error = "tenant_not_resolved", message = result.Reason }, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (httpContext.User.Identity?.IsAuthenticated == true
            && claims.TryGetValue("tenant_slug", out var tokenTenantSlug)
            && !string.Equals(tokenTenantSlug, result.Tenant.Slug, StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(new { error = "tenant_token_mismatch", message = "Token autenticado não pertence ao tenant resolvido." }, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        if (string.Equals(result.Tenant.Status, "SUSPENSO", StringComparison.OrdinalIgnoreCase)
            || string.Equals(result.Tenant.Status, "CANCELADO", StringComparison.OrdinalIgnoreCase))
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsJsonAsync(new { error = "tenant_blocked", status = result.Tenant.Status, message = "Tenant bloqueado para operações." }, httpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        tenantContext.SetTenant(result.Tenant.Id, result.Tenant.Slug, result.Tenant.Status);
        httpContext.Items["TenantId"] = result.Tenant.Id;
        _logger.LogInformation("Tenant resolvido. TenantId={TenantId} TenantSlug={TenantSlug} CorrelationId={CorrelationId}", result.Tenant.Id, result.Tenant.Slug, httpContext.TraceIdentifier);
        await usageMeter.RegistrarRequisicaoAsync(result.Tenant.Id, httpContext.RequestAborted).ConfigureAwait(false);
        await _next(httpContext).ConfigureAwait(false);
    }
}
