using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Transparencia;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro"), Route("api/agro/transparencia")]
public sealed class AgroTransparenciaController : ControllerBase
{
    private readonly IAgroTransparenciaService _service;
    public AgroTransparenciaController(IAgroTransparenciaService service) => _service = service;
    [HttpGet("datasets")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AgroDatasetPublicoResponse>>>> Datasets([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => FromResult(await _service.ListarDatasetsAsync(page, pageSize, ct).ConfigureAwait(false));
    [HttpPost("datasets")] public async Task<ActionResult<ApiResponse<AgroDatasetPublicoResponse>>> Criar([FromBody] AgroDatasetPublicoCreateRequest request, CancellationToken ct) => FromResult(await _service.CriarDatasetAsync(request, ct).ConfigureAwait(false));
    [HttpPost("datasets/{id:long}/publicar")] public async Task<ActionResult<ApiResponse<AgroDatasetPublicacaoResponse>>> Publicar(long id, [FromBody] PublicarAgroDatasetRequest request, CancellationToken ct) => FromResult(await _service.PublicarAsync(id, request, ct).ConfigureAwait(false));
    [HttpPost("datasets/{id:long}/suspender")] public async Task<ActionResult<ApiResponse<AgroDatasetPublicacaoResponse>>> Suspender(long id, CancellationToken ct) => FromResult(await _service.SuspenderAsync(id, ct).ConfigureAwait(false));
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result) { if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value)); if (result.Error == "403") return Forbid(); if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error)); return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida.")); }
}
