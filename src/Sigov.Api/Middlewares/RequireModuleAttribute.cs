using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Application.Saas;
using Sigov.Application.Saas.Modules;

namespace Sigov.Api.Middlewares;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireModuleAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _moduleCode;

    public RequireModuleAttribute(string moduleCode) => _moduleCode = moduleCode;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
        if (!tenantContext.TenantId.HasValue)
        {
            context.Result = new ObjectResult(new { message = "Tenant obrigatório para acesso ao módulo." }) { StatusCode = StatusCodes.Status400BadRequest };
            return;
        }

        var user = context.HttpContext.User;
        if (!long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("usuario_id"), out var userId) || userId <= 0)
        {
            context.Result = new ObjectResult(new { message = "Identidade obrigatória para acesso ao módulo." }) { StatusCode = StatusCodes.Status401Unauthorized };
            return;
        }

        var evaluator = context.HttpContext.RequestServices.GetRequiredService<IModuleEntitlementEvaluator>();
        var profiles = user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        var decision = await evaluator.EvaluateAsync(new ModuleEntitlementRequest(
            userId, _moduleCode, profiles, tenantContext.TenantId, CorrelationId: context.HttpContext.TraceIdentifier),
            context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (!decision.Allowed)
        {
            context.Result = new ObjectResult(new { message = decision.Reason }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next().ConfigureAwait(false);
    }
}
