using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Geo;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule("agro")]
[Route("api/agro/geo")]
public sealed class AgroGeoController : ControllerBase
{
    private readonly IAgroGeoService _service;

    public AgroGeoController(IAgroGeoService service) => _service = service;

    [HttpGet("camadas")]
    public async Task<ActionResult<ApiResponse<PagedResult<AgroGeoCamadaResponse>>>> ListarCamadas([FromQuery] AgroGeoFiltro filtro, CancellationToken cancellationToken) => FromResult(await _service.ListarCamadasAsync(filtro, cancellationToken).ConfigureAwait(false));

    [HttpGet("camadas/{id:long}")]
    public async Task<ActionResult<ApiResponse<AgroGeoCamadaResponse>>> ObterCamada(long id, CancellationToken cancellationToken) => FromResult(await _service.ObterCamadaAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpPost("camadas")]
    public async Task<ActionResult<ApiResponse<long>>> CriarCamada([FromBody] AgroGeoCamadaRequest request, CancellationToken cancellationToken) => FromResult(await _service.CriarCamadaAsync(request, cancellationToken).ConfigureAwait(false));

    [HttpPut("camadas/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarCamada(long id, [FromBody] AgroGeoCamadaRequest request, CancellationToken cancellationToken) => FromResult(await _service.AtualizarCamadaAsync(id, request, cancellationToken).ConfigureAwait(false));

    [HttpDelete("camadas/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> ExcluirCamada(long id, CancellationToken cancellationToken) => FromResult(await _service.ExcluirCamadaAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpGet("feicoes")]
    public async Task<ActionResult<ApiResponse<PagedResult<AgroGeoFeicaoResponse>>>> ListarFeicoes([FromQuery] AgroGeoFiltro filtro, CancellationToken cancellationToken) => FromResult(await _service.ListarFeicoesAsync(filtro, cancellationToken).ConfigureAwait(false));

    [HttpGet("feicoes/{id:long}")]
    public async Task<ActionResult<ApiResponse<AgroGeoFeicaoResponse>>> ObterFeicao(long id, CancellationToken cancellationToken) => FromResult(await _service.ObterFeicaoAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpPost("feicoes")]
    public async Task<ActionResult<ApiResponse<long>>> CriarFeicao([FromBody] AgroGeoFeicaoRequest request, CancellationToken cancellationToken) => FromResult(await _service.CriarFeicaoAsync(request, cancellationToken).ConfigureAwait(false));

    [HttpPut("feicoes/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> AtualizarFeicao(long id, [FromBody] AgroGeoFeicaoRequest request, CancellationToken cancellationToken) => FromResult(await _service.AtualizarFeicaoAsync(id, request, cancellationToken).ConfigureAwait(false));

    [HttpDelete("feicoes/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> ExcluirFeicao(long id, CancellationToken cancellationToken) => FromResult(await _service.ExcluirFeicaoAsync(id, cancellationToken).ConfigureAwait(false));

    [HttpGet("export.geojson")]
    public async Task<IActionResult> Exportar(CancellationToken cancellationToken)
    {
        var result = await _service.ExportarGeoJsonAsync(cancellationToken).ConfigureAwait(false);
        if (result.IsFailure) return result.Error == "403" ? Forbid() : BadRequest(ApiResponse<string>.Fail(result.Error ?? "Falha ao exportar GeoJSON."));
        return Content(result.Value ?? "{\"type\":\"FeatureCollection\",\"features\":[]}", "application/geo+json");
    }

    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value));
        if (result.Error == "403") return Forbid();
        if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error));
        if (result.Error?.Contains("não encontrada", StringComparison.OrdinalIgnoreCase) == true) return NotFound(ApiResponse<T>.Fail(result.Error));
        return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida."));
    }

    private ActionResult<ApiResponse<object>> FromResult(Result result)
    {
        if (result.IsSuccess) return Ok(ApiResponse<object>.Ok(new { ok = true }));
        if (result.Error == "403") return Forbid();
        if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<object>.Fail(result.Error));
        return BadRequest(ApiResponse<object>.Fail(result.Error ?? "Requisição inválida."));
    }
}
