using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
using Sigov.Web.Services.Operational;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class TesourariaController : Controller
{
    private readonly TesourariaService _service;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<TesourariaController> _logger;
    public TesourariaController(TesourariaService service, IAuditTrailService auditTrail, ILogger<TesourariaController> logger) { _service = service; _auditTrail = auditTrail; _logger = logger; }

    [HttpGet, Route("/Tesouraria")]
    [Route("/Tesouraria/ContasBancarias")]
    [Route("/Tesouraria/Movimentos")]
    [Route("/Tesouraria/Conciliacao")]
    [Route("/Tesouraria/Arrecadacao")]
    [Route("/Tesouraria/Pagamentos")]
    [Route("/Tesouraria/Relatorios")]
    public async Task<IActionResult> Index(string? q = null, CancellationToken cancellationToken = default)
    {
        var screen = RouteData.Values["action"]?.ToString() ?? "Dashboard";
        return View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Tesouraria", screen, q, cancellationToken).ConfigureAwait(false));
    }

    [HttpGet, Route("/Tesouraria/Detalhes/{id:long}")]
    [HttpGet, Route("/Tesouraria/Solicitacoes/{id:long}")]
    [HttpGet, Route("/Tesouraria/Processos/{id:long}")]
    [HttpGet, Route("/Tesouraria/Bens/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken) => View("~/Views/Operational/Module.cshtml", await _service.BuildAsync("Tesouraria", $"Detalhes #{id}", null, cancellationToken).ConfigureAwait(false));


    private async Task Audit(string acao, string entidade, string? id, CancellationToken ct)
    {
        try { await _auditTrail.RegistrarAsync(null, null, acao, entidade, id, null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, ct).ConfigureAwait(false); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria administrativa em fallback"); }
    }
}
