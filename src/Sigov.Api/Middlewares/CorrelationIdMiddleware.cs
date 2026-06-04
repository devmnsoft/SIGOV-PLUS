namespace Sigov.Api.Middlewares;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var value) && Guid.TryParse(value, out var parsed)
            ? parsed
            : Guid.NewGuid();
        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString();
        await _next(context).ConfigureAwait(false);
    }
}
