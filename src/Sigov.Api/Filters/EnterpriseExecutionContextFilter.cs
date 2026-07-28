using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Api.Contracts;
using Sigov.Application.Enterprise;

namespace Sigov.Api.Filters;

/// <summary>Builds and clears the request-scoped context used by Enterprise repositories.</summary>
public sealed class EnterpriseExecutionContextFilter : IAsyncActionFilter
{
    private static readonly Guid DemoTenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EnterpriseExecutionContextFilter> _logger;

    public EnterpriseExecutionContextFilter(IWebHostEnvironment environment, IConfiguration configuration, ILogger<EnterpriseExecutionContextFilter> logger)
    {
        _environment = environment;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var http = context.HttpContext;
        var tenantId = ResolveTenant(http);
        if (tenantId is null)
        {
            context.Result = new BadRequestObjectResult(ApiResponse<string>.Fail("Tenant obrigatório. Informe X-Tenant-Id válido; fallback demo é proibido em produção.", http.TraceIdentifier));
            return;
        }

        var login = http.User.Identity?.Name ?? http.User.FindFirst("preferred_username")?.Value ?? http.User.FindFirst("sub")?.Value ?? "usuario.autenticado";
        EnterpriseExecutionContextAccessor.Current = new EnterpriseExecutionContext(tenantId.Value, http.User.FindFirst("sub")?.Value ?? login, login, http.Connection.RemoteIpAddress?.ToString(), http.Request.Headers.UserAgent.ToString(), http.TraceIdentifier, http.User.Claims.Select(c => c.Value).ToArray());
        try
        {
            await next().ConfigureAwait(false);
        }
        finally
        {
            EnterpriseExecutionContextAccessor.Current = null;
        }
    }

    private Guid? ResolveTenant(HttpContext context)
    {
        if (Guid.TryParse(context.Request.Headers["X-Tenant-Id"].FirstOrDefault(), out var tenantId)) return tenantId;
        if (!_environment.IsProduction() && _configuration.GetValue<bool>("Enterprise:AllowDemoTenantFallback"))
        {
            _logger.LogWarning("Enterprise usando tenant demo por fallback explícito. Environment={Environment}; CorrelationId={CorrelationId}", _environment.EnvironmentName, context.TraceIdentifier);
            return DemoTenantId;
        }
        return null;
    }
}
