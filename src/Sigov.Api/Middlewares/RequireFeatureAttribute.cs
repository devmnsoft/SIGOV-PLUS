using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Application.Saas;

namespace Sigov.Api.Middlewares;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireFeatureAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _featureCode;

    public RequireFeatureAttribute(string featureCode) => _featureCode = featureCode;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenantContext.TenantId.HasValue)
        {
            context.Result = new ObjectResult(new { message = "Tenant obrigatório para acesso à feature." }) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        var service = context.HttpContext.RequestServices.GetRequiredService<IFeatureFlagService>();
        if (!await service.IsEnabledAsync(tenantContext.TenantId.Value, _featureCode, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Result = new ObjectResult(new { message = "Feature flag desabilitada para o tenant." }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next().ConfigureAwait(false);
    }
}
