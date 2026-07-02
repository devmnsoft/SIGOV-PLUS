using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class FrotasController : Controller
{
    private readonly FrotasService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<FrotasController> _logger;
    public FrotasController(FrotasService service, IAuditTrailService auditTrail, ILogger<FrotasController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Frotas")]
    [Route("/Frotas/Veiculos")]
    [Route("/Frotas/Abastecimentos")]
    [Route("/Frotas/Manutencoes")]
    [Route("/Frotas/Multas")]
    [Route("/Frotas/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Frotas", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Frotas/Detalhes/{id:long}")]
    [HttpGet, Route("/Frotas/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Frotas/Processos/{id:long}")]
    [HttpGet, Route("/Frotas/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Frotas", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));


    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
