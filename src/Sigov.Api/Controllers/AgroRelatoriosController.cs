using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Middlewares;
using Sigov.Application.Agro.Relatorios;
using Sigov.Application.Common;
using Sigov.Domain.Common;

namespace Sigov.Api.Controllers;

[ApiController, Authorize, RequireModule("agro")]
public sealed class AgroRelatoriosController : ControllerBase
{
    private readonly IAgroRelatorioService _relatorios; private readonly IAgroExportService _export;
    public AgroRelatoriosController(IAgroRelatorioService relatorios, IAgroExportService export) { _relatorios = relatorios; _export = export; }
    [HttpGet("api/agro/relatorios/modelos")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AgroRelatorioModeloResponse>>>> Modelos([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => FromResult(await _relatorios.ListarModelosAsync(page, pageSize, ct).ConfigureAwait(false));
    [HttpPost("api/agro/relatorios/modelos")] public async Task<ActionResult<ApiResponse<AgroRelatorioModeloResponse>>> CriarModelo([FromBody] AgroRelatorioModeloCreateRequest request, CancellationToken ct) => FromResult(await _relatorios.CriarModeloAsync(request, ct).ConfigureAwait(false));
    [HttpPost("api/agro/relatorios/modelos/{id:long}/executar")] public async Task<ActionResult<ApiResponse<AgroRelatorioExecucaoResponse>>> Executar(long id, [FromBody] ExecutarAgroRelatorioRequest request, CancellationToken ct) => FromResult(await _relatorios.ExecutarAsync(id, request, ct).ConfigureAwait(false));
    [HttpGet("api/agro/relatorios/execucoes")] public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AgroRelatorioExecucaoResponse>>>> Execucoes([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default) => FromResult(await _relatorios.ListarExecucoesAsync(page, pageSize, ct).ConfigureAwait(false));
    [HttpGet("api/agro/relatorios/execucoes/{id:long}")] public async Task<ActionResult<ApiResponse<AgroRelatorioExecucaoResponse>>> Execucao(long id, CancellationToken ct) => FromResult(await _relatorios.ObterExecucaoAsync(id, ct).ConfigureAwait(false));
    [HttpGet("api/agro/export/{dataset}.csv")] public Task<IActionResult> ExportCsv(string dataset, CancellationToken ct) => Export(dataset, "CSV", ct);
    [HttpGet("api/agro/export/{dataset}.json")] public Task<IActionResult> ExportJson(string dataset, CancellationToken ct) => Export(dataset, "JSON", ct);
    [HttpGet("api/agro/export/geojson")] public Task<IActionResult> ExportGeoJson(CancellationToken ct) => Export("geojson", "GEOJSON", ct);
    private async Task<IActionResult> Export(string dataset, string formato, CancellationToken ct) { var result = await _export.ExportarAsync(new AgroExportRequest(dataset, formato), ct).ConfigureAwait(false); if (result.IsFailure || result.Value is null) return result.Error == "403" ? Forbid() : BadRequest(ApiResponse<object>.Fail(result.Error ?? "Exportação inválida.")); return File(System.Text.Encoding.UTF8.GetBytes(result.Value.Content), result.Value.ContentType, result.Value.FileName); }
    private ActionResult<ApiResponse<T>> FromResult<T>(Result<T> result) { if (result.IsSuccess && result.Value is not null) return Ok(ApiResponse<T>.Ok(result.Value)); if (result.Error == "403") return Forbid(); if (result.Error?.Contains("autenticado", StringComparison.OrdinalIgnoreCase) == true) return Unauthorized(ApiResponse<T>.Fail(result.Error)); return BadRequest(ApiResponse<T>.Fail(result.Error ?? "Requisição inválida.")); }
}
