using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Saas.Context;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/saas/contexto")]
public sealed class TenantContextController : ControllerBase
{
    private readonly ITenantContextSwitchRepository _repository;
    private readonly ITenantContextSwitcher _switcher;

    public TenantContextController(ITenantContextSwitcher switcher, ITenantContextSwitchRepository repository)
    {
        _switcher = switcher;
        _repository = repository;
    }

    [HttpPost("trocar")]
    public async Task<ActionResult<ApiResponse<TenantContextSwitchResult>>> Switch(TenantContextSwitchRequest request, CancellationToken cancellationToken)
    {
        var enriched = request with { Ip = request.Ip ?? HttpContext.Connection.RemoteIpAddress?.ToString(), UserAgent = request.UserAgent ?? Request.Headers["User-Agent"].ToString(), CorrelationId = request.CorrelationId ?? CurrentCorrelationId() };
        var result = await _switcher.SwitchAsync(enriched, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(ApiResponse<TenantContextSwitchResult>.Ok(result)) : StatusCode(StatusCodes.Status403Forbidden, ApiResponse<TenantContextSwitchResult>.Fail(result.Message));
    }

    [HttpPost("finalizar")]
    public async Task<ActionResult<ApiResponse<TenantContextSwitchResult>>> Finish(FinishTenantContextRequest request, CancellationToken cancellationToken)
    {
        var result = await _switcher.FinishAsync(request.LogId, request.UsuarioGlobalId, cancellationToken).ConfigureAwait(false);
        return result.Success ? Ok(ApiResponse<TenantContextSwitchResult>.Ok(result)) : StatusCode(StatusCodes.Status403Forbidden, ApiResponse<TenantContextSwitchResult>.Fail(result.Message));
    }

    [HttpGet("logs")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<TenantContextLogItem>>>> Logs([FromQuery] long? usuarioGlobalId, [FromQuery] long? tenantId, CancellationToken cancellationToken)
    {
        var logs = await _repository.GetLogsAsync(usuarioGlobalId, tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<IReadOnlyCollection<TenantContextLogItem>>.Ok(logs));
    }

    private Guid? CurrentCorrelationId() => Guid.TryParse(HttpContext.TraceIdentifier, out var id) ? id : null;
}

public sealed record FinishTenantContextRequest(long LogId, long UsuarioGlobalId);
