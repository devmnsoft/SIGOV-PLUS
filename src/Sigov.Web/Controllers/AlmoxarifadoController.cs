using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class AlmoxarifadoController : Controller
{
    private readonly AlmoxarifadoService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<AlmoxarifadoController> _logger;
    public AlmoxarifadoController(AlmoxarifadoService service, IAuditTrailService auditTrail, ILogger<AlmoxarifadoController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Almoxarifado")]
    [Route("/Estoque/Dashboard")]
    [Route("/Almoxarifado/Produtos")]
    [Route("/Almoxarifado/Entradas")]
    [Route("/Almoxarifado/Saidas")]
    [Route("/Almoxarifado/Movimentos")]
    [Route("/Almoxarifado/Inventario")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Almoxarifado", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Almoxarifado/Detalhes/{id:long}")]
    [HttpGet, Route("/Almoxarifado/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Almoxarifado/Processos/{id:long}")]
    [HttpGet, Route("/Almoxarifado/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Almoxarifado", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));


    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
