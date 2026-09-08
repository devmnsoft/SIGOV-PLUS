using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Api.Middlewares;
using Sigov.Application.Abstractions;
using Sigov.Application.Authorization;
using Sigov.Application.Industria;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/comercio")]
[RequireModule("comercial")]
public sealed class IndustriaComercialController : ControllerBase
{
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    private readonly IIndustriaComercialService _industriaComercial;
    private readonly IAuthorizationEvaluator _authorization;
    private readonly ILogger<IndustriaComercialController> _logger;

    public IndustriaComercialController(ICurrentTenant tenant, ICurrentUser user, IIndustriaComercialService industriaComercial, IAuthorizationEvaluator authorization, ILogger<IndustriaComercialController> logger)
    {
        _tenant = tenant;
        _user = user;
        _industriaComercial = industriaComercial;
        _authorization = authorization;
        _logger = logger;
    }

    [HttpPost("pedidos/{id:long}/gerar-op")]
    public async Task<ActionResult<ApiResponse<object>>> GerarOp(long id)
    {
        var cid = HttpContext.TraceIdentifier;
        try
        {
            if (!await HasPermission("industria.ordens.criar")) return Forbid();
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

    private async Task<bool> HasPermission(string permission)
    {
        if (User.Identity?.IsAuthenticated != true || !_user.UsuarioId.HasValue || !_tenant.TenantId.HasValue)
            return false;

        var separator = permission.LastIndexOf('.');
        if (separator <= 0 || separator == permission.Length - 1)
            return false;

        var qualifiedResource = permission[..separator];
        var moduleSeparator = qualifiedResource.IndexOf('.');
        var module = moduleSeparator > 0 ? qualifiedResource[..moduleSeparator] : "industria";
        var resource = moduleSeparator > 0 ? qualifiedResource[(moduleSeparator + 1)..] : qualifiedResource;
        var decision = await _authorization.EvaluateAsync(new AuthorizationRequest(
            _user.UsuarioId.Value,
            module,
            resource,
            permission[(separator + 1)..],
            _tenant.TenantId,
            _tenant.EntidadeId,
            _tenant.ExercicioId,
            CorrelationId: HttpContext.TraceIdentifier,
            Origem: "API_INDUSTRIA_COMERCIAL"), HttpContext.RequestAborted).ConfigureAwait(false);
        return decision.Permitido;
    }
}
