using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.Parameters;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/saas/parametros")]
public sealed class TenantParametersController : ControllerBase
{
    private readonly ITenantParameterResolver _resolver;
    private readonly ITenantParameterService _service;

    public TenantParametersController(ITenantParameterService service, ITenantParameterResolver resolver)
    {
        _service = service;
        _resolver = resolver;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantParameterDefinitionDto>>>> Get(CancellationToken cancellationToken)
    {
        var definitions = await _service.GetDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantParameterDefinitionDto>>.Ok(definitions));
    }

    [HttpGet("{codigo}")]
    public async Task<ActionResult<ApiResponse<object>>> GetByCode(string codigo, [FromQuery] long? tenantId, [FromQuery] long? entidadeId, [FromQuery] long? exercicioId, [FromQuery] string? moduloCodigo, CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            var definition = await _service.GetDefinitionAsync(codigo, cancellationToken).ConfigureAwait(false);
            return definition is null ? NotFound(ApiResponse<object>.Fail("Parâmetro não encontrado.")) : Ok(ApiResponse<object>.Ok(definition));
        }

        var resolved = await _resolver.ResolveAsync(codigo, new TenantParameterResolveContext(tenantId.Value, entidadeId, exercicioId, CurrentUserId(), moduloCodigo), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(resolved));
    }

    [HttpPut("{codigo}")]
    public async Task<ActionResult<ApiResponse<object>>> Put(string codigo, TenantParameterValueDto value, CancellationToken cancellationToken)
    {
        await _service.SaveValueAsync(codigo, value, CurrentUserId(), CurrentCorrelationId(), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { codigo, value.TenantId, value.Escopo }));
    }

    private long? CurrentUserId() => long.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst("usuario_id")?.Value, out var id) ? id : null;

    private Guid? CurrentCorrelationId() => Guid.TryParse(HttpContext.TraceIdentifier, out var id) ? id : null;
}
