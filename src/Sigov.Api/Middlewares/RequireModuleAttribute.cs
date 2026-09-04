using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sigov.Application.Saas;

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

        var service = context.HttpContext.RequestServices.GetRequiredService<IModuloLicenciamentoService>();
        if (!await service.IsModuleEnabledAsync(tenantContext.TenantId.Value, _moduleCode, context.HttpContext.RequestAborted).ConfigureAwait(false))
        {
            context.Result = new ObjectResult(new { message = "Módulo não contratado ou desabilitado para o tenant." }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next().ConfigureAwait(false);
    }
}
