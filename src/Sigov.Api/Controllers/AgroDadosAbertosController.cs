using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Agro.Transparencia;
using Sigov.Application.Common;

namespace Sigov.Api.Controllers;

[ApiController, Route("api/publico/agro/{tenantSlug}")]
// Endpoints: api/publico/agro/{tenantSlug}/datasets e downloads publicados.
public sealed class AgroDadosAbertosController : ControllerBase
{
    private readonly IAgroTransparenciaService _service;
    public AgroDadosAbertosController(IAgroTransparenciaService service) => _service = service;
    [HttpGet("datasets")] public async Task<IActionResult> Datasets(string tenantSlug, CancellationToken ct) { var result = await _service.ListarPublicosAsync(tenantSlug, ct).ConfigureAwait(false); return result.IsSuccess ? Ok(ApiResponse<IReadOnlyCollection<AgroDatasetPublicoResponse>>.Ok(result.Value!)) : NotFound(ApiResponse<object>.Fail(result.Error ?? "Não encontrado.")); }
    [HttpGet("datasets/{codigo}/download.csv")] public Task<IActionResult> Csv(string tenantSlug, string codigo, CancellationToken ct) => Download(tenantSlug, codigo, "CSV", "text/csv", $"{codigo}.csv", ct);
    [HttpGet("datasets/{codigo}/download.json")] public Task<IActionResult> DownloadJson(string tenantSlug, string codigo, CancellationToken ct) => Download(tenantSlug, codigo, "JSON", "application/json", $"{codigo}.json", ct);
    [HttpGet("datasets/{codigo}/download.geojson")] public Task<IActionResult> GeoJson(string tenantSlug, string codigo, CancellationToken ct) => Download(tenantSlug, codigo, "GEOJSON", "application/geo+json", $"{codigo}.geojson", ct);
    private async Task<IActionResult> Download(string tenantSlug, string codigo, string formato, string contentType, string fileName, CancellationToken ct) { var result = await _service.DownloadPublicoAsync(tenantSlug, codigo, formato, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].ToString(), ct).ConfigureAwait(false); if (result.IsFailure || result.Value is null) return NotFound(ApiResponse<object>.Fail(result.Error ?? "Dataset publicado não encontrado.")); return File(System.Text.Encoding.UTF8.GetBytes(result.Value.ConteudoTexto ?? string.Empty), contentType, fileName); }
}
