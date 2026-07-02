using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SiaficController : Controller
{
    private readonly SiaficService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<SiaficController> _logger;
    public SiaficController(SiaficService service, IAuditTrailService auditTrail, ILogger<SiaficController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Siafic")]
    [Route("/Siafic/Dashboard")]
    [Route("/Siafic/PlanoContas")]
    [Route("/Siafic/Dotacoes")]
    [Route("/Siafic/Empenhos")]
    [Route("/Siafic/Liquidacoes")]
    [Route("/Siafic/Pagamentos")]
    [Route("/Siafic/Receitas")]
    [Route("/Siafic/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Siafic", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Siafic/Detalhes/{id:long}")]
    [HttpGet, Route("/Siafic/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Siafic/Processos/{id:long}")]
    [HttpGet, Route("/Siafic/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Siafic", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));


    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
