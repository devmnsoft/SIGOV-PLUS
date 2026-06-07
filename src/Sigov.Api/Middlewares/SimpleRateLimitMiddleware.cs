using System.Collections.Concurrent;
using Sigov.Application.Configuration;
using Sigov.Application.Saas;
using Microsoft.Extensions.Options;

namespace Sigov.Api.Middlewares;

public sealed class SimpleRateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, Counter> Counters = new();
    private readonly RequestDelegate _next;
    private readonly ILogger<SimpleRateLimitMiddleware> _logger;

    public SimpleRateLimitMiddleware(RequestDelegate next, ILogger<SimpleRateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, IOptions<SigovOptions> options)
    {
        if (IsHealthEndpoint(context.Request.Path))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var limit = ResolveLimit(context.Request.Path, options.Value);
        var key = ResolveKey(context, tenantContext);
        var now = DateTimeOffset.UtcNow;
        var counter = Counters.AddOrUpdate(key,
            _ => new Counter(now, 1),
            (_, existing) => existing.WindowStart.AddMinutes(1) <= now ? new Counter(now, 1) : existing.Increment());

        if (counter.Count > limit)
        {
            _logger.LogWarning("Rate limit excedido. Key={RateLimitKey} Path={Path} Limit={Limit}", key, context.Request.Path, limit);
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "rate_limited", message = "Limite de requisições excedido para o tenant/IP." }, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private static bool IsHealthEndpoint(PathString path) => path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase);

    private static int ResolveLimit(PathString path, SigovOptions options)
    {
        if (path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase) || path.StartsWithSegments("/api/login", StringComparison.OrdinalIgnoreCase))
        {
            return options.RateLimit.LoginAttemptsPerMinute;
        }

        return options.RateLimit.RequestsPerMinutePerTenant;
    }

    private static string ResolveKey(HttpContext context, ITenantContext tenantContext) => tenantContext.TenantId.HasValue
        ? $"tenant:{tenantContext.TenantId.Value}"
        : $"ip:{context.Connection.RemoteIpAddress}";

    private sealed record Counter(DateTimeOffset WindowStart, int Count)
    {
        public Counter Increment() => this with { Count = Count + 1 };
    }
}
