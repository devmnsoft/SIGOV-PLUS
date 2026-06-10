using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Industria;
using System.Security.Claims;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/comercio")]
[RequireModule("comercial")]
public sealed class IndustriaComercialController : ControllerBase
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IIndustriaComercialService _industriaComercial;
    private readonly ILogger<IndustriaComercialController> _logger;

    public IndustriaComercialController(ICurrentTenant tenant, ICurrentUser user, IIndustriaComercialService industriaComercial, ILogger<IndustriaComercialController> logger)
    {
        _tenant = tenant;
        _user = user;
        _industriaComercial = industriaComercial;
        _logger = logger;
    }

    [HttpPost("pedidos/{id:long}/gerar-op")]
    public async Task<ActionResult<ApiResponse<object>>> GerarOp(long id)
    {
        var cid = HttpContext.TraceIdentifier;
        try
        {
            if (!HasPermission("industria.ordens.criar")) return Forbid();
            var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("tenant_id obrigatório para gerar OP.");
            var ordemId = await _industriaComercial.GerarOrdemProducaoDoPedidoAsync(tenantId, id, _user.UsuarioId, cid, HttpContext.RequestAborted);
            return Ok(ApiResponse<object>.Ok(new { pedidoId = id, ordemId }, "Pedido gerou ordem de produção.", cid));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Regra de geração de OP rejeitada. CorrelationId={CorrelationId}", cid);
            return UnprocessableEntity(ApiResponse<object>.Fail(ex.Message, cid));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar OP do pedido. CorrelationId={CorrelationId}", cid);
            return StatusCode(500, ApiResponse<object>.Fail("Falha ao gerar OP do pedido.", cid));
        }
    }

    private bool HasPermission(string permission) => User.Identity?.IsAuthenticated != true || User.IsInRole("ADMIN_GERAL") || User.IsInRole("ADMIN_TENANT") || User.Claims.Any(c => (c.Type == "permission" || c.Type == ClaimTypes.Role) && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase));
}
