using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Dashboard;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule("agro")]
[Route("api/agro/dashboard")]
public sealed class AgroDashboardController : ControllerBase
{
    private readonly IAgroDashboardService _service;

    public AgroDashboardController(IAgroDashboardService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<ApiResponse<AgroDashboardResponse>>> Obter(CancellationToken cancellationToken) => FromResult(await _service.ObterAsync(cancellationToken).ConfigureAwait(false));

    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value));
        if (result.Error == "403") return Forbid();
        if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error));
        return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida."));
    }
}
