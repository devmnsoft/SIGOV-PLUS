using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers.OrdemServico;
[Authorize]
public sealed class OrdemServicoController : Controller
{
    [HttpGet("/OrdemServico"),HttpGet("/OrdemServico/Ordens")]
    [Authorize(Policy="os.ordens.visualizar")]
    public IActionResult Ordens()=>View("~/Views/OrdemServico/Ordens/Index.cshtml");
    [HttpGet("/OrdemServico/Agenda")]
    [Authorize(Policy="os.ordens.agendar")]
    public IActionResult Agenda()=>View("~/Views/OrdemServico/Agenda/Index.cshtml");
    [HttpGet("/OrdemServico/Dashboard")]
    [Authorize(Policy="os.dashboard.visualizar")]
    public IActionResult Dashboard()=>View("~/Views/OrdemServico/Dashboard/Index.cshtml");
    [HttpGet("/OrdemServico/Ordens/{id:guid}")]
    [Authorize(Policy="os.ordens.visualizar")]
    public IActionResult Detalhe(Guid id)=>View("~/Views/Tecnico/Ordens/Detalhe.cshtml",id);

    [HttpGet("/api/ordens-servico/{id:guid}/historico")]
    [Authorize(Policy="os.ordens.visualizar")]
    public async Task<IActionResult> Historico(Guid id, [FromServices] Sigov.Application.OrdemServico.IOrdemServicoApplicationService service, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst("enterprise_tenant_id")?.Value ?? User.FindFirst("tenant_id")?.Value, out var t) || t == Guid.Empty)
            return Unauthorized();
        if (!Guid.TryParse(User.FindFirst("sub")?.Value ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u) || u == Guid.Empty)
            return Unauthorized();
        var ctx = new Sigov.Application.OrdemServico.OrdemServicoContext(t, u, HttpContext.TraceIdentifier);
        return Ok(await service.HistoricoAsync(ctx, id, ct));
    }
}
