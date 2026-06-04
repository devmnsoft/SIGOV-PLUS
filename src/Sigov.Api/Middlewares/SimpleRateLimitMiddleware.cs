using System.Collections.Concurrent;
using Sigov.Application.Configuration;
using Sigov.Application.Saas;
using Microsoft.Extensions.Options;

namespace Sigov.Api.Middlewares;

public sealed class SimpleRateLimitMiddleware
{
    private static readonly ConcurrentDictionary<string, Counter> Counters = new();
    private readonly RequestDelegate _next;

    public SimpleRateLimitMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, IOptions<SigovOptions> options)
    {
        var limit = options.Value.RateLimit.RequestsPerMinutePerTenant;
        var key = tenantContext.TenantId.HasValue
            ? $"tenant:{tenantContext.TenantId.Value}"
            : $"ip:{context.Connection.RemoteIpAddress}";
        var now = DateTimeOffset.UtcNow;
        var counter = Counters.AddOrUpdate(key,
            _ => new Counter(now, 1),
            (_, existing) => existing.WindowStart.AddMinutes(1) <= now ? new Counter(now, 1) : existing.Increment());

        if (counter.Count > limit)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsJsonAsync(new { error = "rate_limited", message = "Limite de requisições excedido para o tenant/IP." }, context.RequestAborted).ConfigureAwait(false);
            return;
        }

        await _next(context).ConfigureAwait(false);
    }

    private sealed record Counter(DateTimeOffset WindowStart, int Count)
    {
        public Counter Increment() => this with { Count = Count + 1 };
    }
}
