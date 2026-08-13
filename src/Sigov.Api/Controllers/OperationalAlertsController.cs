using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Abstractions;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/alertas-operacionais")]
public sealed class OperationalAlertsController : ControllerBase
{
    private readonly IOperationalImportStore _store;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    public OperationalAlertsController(IOperationalImportStore store, ICurrentTenant tenant, ICurrentUser user) { _store=store; _tenant=tenant; _user=user; }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OperationalAlertResponse>>>> List([FromQuery] string? module, [FromQuery] string? severity, CancellationToken ct)
    {
        var tenantId=_tenant.TenantId??0; if(tenantId<=0) return BadRequest(ApiResponse<IReadOnlyCollection<OperationalAlertResponse>>.Fail("Tenant obrigatório."));
        return Ok(ApiResponse<IReadOnlyCollection<OperationalAlertResponse>>.Ok(await _store.ListAlertsAsync(tenantId,module?.ToUpperInvariant(),severity?.ToUpperInvariant(),ct).ConfigureAwait(false)));
    }

    [HttpPost("{id:long}/resolver")]
    public async Task<ActionResult<ApiResponse<object>>> Resolve(long id, ResolveOperationalAlertRequest request, CancellationToken ct)
    {
        var tenantId=_tenant.TenantId??0; if(tenantId<=0) return BadRequest(ApiResponse<object>.Fail("Tenant obrigatório."));
        if(string.IsNullOrWhiteSpace(request.Justification)) return BadRequest(ApiResponse<object>.Fail("Justificativa obrigatória."));
        var changed=await _store.ResolveAlertAsync(tenantId,id,_user.UsuarioId,request.Justification,HttpContext.TraceIdentifier,ct).ConfigureAwait(false);
        return changed ? Ok(ApiResponse<object>.Ok(new { id, status="RESOLVIDO" })) : NotFound(ApiResponse<object>.Fail("Alerta não encontrado ou já resolvido."));
    }
}
