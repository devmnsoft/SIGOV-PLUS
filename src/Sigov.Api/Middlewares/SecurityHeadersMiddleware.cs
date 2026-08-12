namespace Sigov.Api.Middlewares;

public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        // Swagger UI uses an inline bootstrap script and inline styles in its generated
        // index. Keep the strict policy for the API and relax only those two directives
        // for Swagger; otherwise the browser renders a blank page while swagger.json is healthy.
        context.Response.Headers["Content-Security-Policy"] = context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            ? "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; object-src 'none'; frame-ancestors 'none'; base-uri 'self'"
            : "default-src 'self'; object-src 'none'; frame-ancestors 'none'; base-uri 'self'";
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        if (_environment.IsProduction() && context.Request.IsHttps)
        {
            context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        }

        await _next(context).ConfigureAwait(false);
    }
}
