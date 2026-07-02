using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TransparenciaController : Controller
{
    private readonly TransparenciaService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<TransparenciaController> _logger;
    public TransparenciaController(TransparenciaService service, IAuditTrailService auditTrail, ILogger<TransparenciaController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Transparencia")]
    [Route("/Transparencia/Receitas")]
    [Route("/Transparencia/Despesas")]
    [Route("/Transparencia/Contratos")]
    [Route("/Transparencia/Licitacoes")]
    [Route("/Transparencia/Servidores")]
    [Route("/Transparencia/Obras")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Transparencia", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Transparencia/Detalhes/{id:long}")]
    [HttpGet, Route("/Transparencia/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Transparencia/Processos/{id:long}")]
    [HttpGet, Route("/Transparencia/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Transparencia", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));


    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
