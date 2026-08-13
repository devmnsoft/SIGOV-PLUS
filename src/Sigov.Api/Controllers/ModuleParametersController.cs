using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Abstractions;
using Sigov.Application.Parameters;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/parametros")]
public sealed class ModuleParametersController : ControllerBase
{
    private readonly IModuleParameterService _service;
    private readonly ICurrentTenant _tenant;
    private readonly ICurrentUser _user;
    public ModuleParametersController(IModuleParameterService service, ICurrentTenant tenant, ICurrentUser user) { _service = service; _tenant = tenant; _user = user; }

    [HttpGet("{modulo}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ModuleParameterValue>>>> Get(string modulo, CancellationToken cancellationToken)
        => Ok(ApiResponse<IReadOnlyCollection<ModuleParameterValue>>.Ok(await _service.ListAsync(TenantId(), modulo, cancellationToken).ConfigureAwait(false)));

    [HttpPut("{modulo}")]
    public async Task<ActionResult<ApiResponse<object>>> Put(string modulo, SaveModuleParametersRequest request, CancellationToken cancellationToken)
    {
        await _service.SaveAsync(TenantId(), modulo, request, _user.UsuarioId, HttpContext.TraceIdentifier, cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<object>.Ok(new { modulo = modulo.ToUpperInvariant(), updated = request.Values.Count }));
    }

    [HttpGet("{modulo}/historico")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ModuleParameterHistory>>>> History(string modulo, [FromQuery] int page = 1, [FromQuery] int pageSize = 30, CancellationToken cancellationToken = default)
        => Ok(ApiResponse<IReadOnlyCollection<ModuleParameterHistory>>.Ok(await _service.HistoryAsync(TenantId(), modulo, page, pageSize, cancellationToken).ConfigureAwait(false)));

    private long TenantId() => _tenant.TenantId is > 0 ? _tenant.TenantId.Value : throw new InvalidOperationException("Tenant obrigatório.");
}
