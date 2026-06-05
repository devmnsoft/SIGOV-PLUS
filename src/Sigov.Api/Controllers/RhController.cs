using Microsoft.AspNetCore.Mvc;
using Sigov.Api.Contracts;
using Sigov.Application.Common;
using Sigov.Application.Rh;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/rh")]
public sealed class RhController : ProcessosControllerBase
{
    private readonly IRhService _service;
    public RhController(IRhService service) => _service = service;

    [HttpGet("{recurso}")]
    public async Task<ActionResult<ApiResponse<PagedResult<RhRegistroResponse>>>> Listar(string recurso, [FromQuery] RhFiltro filtro, CancellationToken ct) =>
        FromResult(await _service.ListarAsync(recurso, filtro, ct).ConfigureAwait(false));

    [HttpGet("{recurso}/{id:long}")]
    public async Task<ActionResult<ApiResponse<RhRegistroResponse>>> Obter(string recurso, long id, CancellationToken ct) =>
        FromResult(await _service.ObterAsync(recurso, id, ct).ConfigureAwait(false));

    [HttpPost("{recurso}")]
    public async Task<ActionResult<ApiResponse<long>>> Criar(string recurso, [FromBody] RhRegistroCreateRequest request, CancellationToken ct) =>
        FromResult(await _service.CriarAsync(recurso, request, ct).ConfigureAwait(false));

    [HttpPut("{recurso}/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Atualizar(string recurso, long id, [FromBody] RhRegistroUpdateRequest request, CancellationToken ct) =>
        FromResult(await _service.AtualizarAsync(recurso, id, request, ct).ConfigureAwait(false));

    [HttpDelete("{recurso}/{id:long}")]
    public async Task<ActionResult<ApiResponse<object>>> Excluir(string recurso, long id, CancellationToken ct) =>
        FromResult(await _service.ExcluirAsync(recurso, id, ct).ConfigureAwait(false));

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<RhDashboardResponse>>> Dashboard(CancellationToken ct) =>
        FromResult(await _service.DashboardAsync(ct).ConfigureAwait(false));

    [HttpGet("portal/servidores/{servidorId:long}")]
    public async Task<ActionResult<ApiResponse<RhPortalResumoResponse>>> Portal(long servidorId, CancellationToken ct) =>
        FromResult(await _service.PortalServidorAsync(servidorId, ct).ConfigureAwait(false));

    [HttpPost("folhas/integrar-financeiro")]
    public async Task<ActionResult<ApiResponse<long>>> IntegrarFinanceiro([FromBody] RhFinanceiroIntegracaoRequest request, CancellationToken ct) =>
        FromResult(await _service.IntegrarFinanceiroAsync(request, ct).ConfigureAwait(false));

    [HttpGet("export/{recurso}.{formato}")]
    public async Task<IActionResult> Exportar(string recurso, string formato, CancellationToken ct)
    {
        var result = await _service.ExportarAsync(recurso, formato, ct).ConfigureAwait(false);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error ?? "Falha na exportação."));
        return File(result.Value ?? Array.Empty<byte>(), formato.Equals("json", StringComparison.OrdinalIgnoreCase) ? "application/json" : "text/csv", $"rh-{recurso}.{formato}");
    }
}
