using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Bi;
using Sigov.Application.Agro.Relatorios;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro"), Route("api/agro/bi")]
public sealed class AgroBiController : ControllerBase
{
    private readonly IAgroBiService _bi; private readonly IAgroIndicadorService _indicadores;
    public AgroBiController(IAgroBiService bi, IAgroIndicadorService indicadores) { _bi = bi; _indicadores = indicadores; }
    [HttpGet("dashboard")] public async Task<ActionResult<ApiResponse<AgroBiDashboardResponse>>> Dashboard(CancellationToken ct) => FromResult(await _bi.ObterDashboardAsync(ct).ConfigureAwait(false));
    [HttpGet("indicadores")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AgroIndicadorResponse>>>> Indicadores([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => FromResult(await _indicadores.ListarAsync(page, pageSize, ct).ConfigureAwait(false));
    [HttpPost("indicadores")] public async Task<ActionResult<ApiResponse<AgroIndicadorResponse>>> CriarIndicador([FromBody] AgroIndicadorCreateRequest request, CancellationToken ct) => FromResult(await _indicadores.CriarAsync(request, ct).ConfigureAwait(false));
    [HttpGet("indicadores/{id:long}/valores")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AgroIndicadorValorResponse>>>> Valores(long id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => FromResult(await _indicadores.ListarValoresAsync(id, page, pageSize, ct).ConfigureAwait(false));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result) { if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value)); if (result.Error == "403") return Forbid(); if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error)); return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida.")); }
}
