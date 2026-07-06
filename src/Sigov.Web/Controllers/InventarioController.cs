using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class InventarioController : Controller
{
    private readonly InventarioService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<InventarioController> _logger;
    public InventarioController(InventarioService service, IAuditTrailService auditTrail, ILogger<InventarioController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet("/Inventario")]
    [HttpGet("/Inventario/Campanhas")]
    [HttpGet("/Inventario/Campanhas/Nova")]
    [HttpGet("/Inventario/Divergencias")]
    [HttpGet("/Inventario/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Inventario", Request.Path.Value?.Split('/').LastOrDefault() ?? "Dashboard", q, cancellationToken).ConfigureAwait(false));

    [HttpGet("/Inventario/Campanhas/{id:long}")]
    [HttpGet("/Inventario/Campanhas/{id:long}/Itens")]
    public async Task<IActionResult> Campanha(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Inventario", $"Campanha #{id}", null, cancellationToken).ConfigureAwait(false));

    [HttpPost("/Inventario/Campanhas/Nova")]
    [HttpPost("/Inventario/Campanhas/{id:long}/Concluir")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(long? id, CancellationToken cancellationToken)
    {
        await Audit("patrimonio_inventario.acao", "patrimonio_inventario", id?.ToString(), cancellationToken).ConfigureAwait(false);
        TempData["Warning"] = "Inventário não foi salvo/concluído: schema físico e regra oficial ainda não homologados neste ambiente.";
        return Redirect("/Inventario");
    }

    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria de inventário em fallback"); }
    }
}
