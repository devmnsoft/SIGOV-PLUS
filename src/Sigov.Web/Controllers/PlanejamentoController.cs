using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class PlanejamentoController : Controller
{
    private readonly PlanejamentoService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<PlanejamentoController> _logger;
    public PlanejamentoController(PlanejamentoService service, IAuditTrailService auditTrail, ILogger<PlanejamentoController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Planejamento")]
    [Route("/Planejamento/Ppa")]
    [Route("/Planejamento/Ldo")]
    [Route("/Planejamento/Loa")]
    [Route("/Planejamento/Programas")]
    [Route("/Planejamento/Acoes")]
    [Route("/Planejamento/AlteracoesOrcamentarias")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Planejamento", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Planejamento/Detalhes/{id:long}")]
    [HttpGet, Route("/Planejamento/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Planejamento/Processos/{id:long}")]
    [HttpGet, Route("/Planejamento/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Planejamento", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));


    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
