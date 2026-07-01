using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class JuridicoController : Controller
{
    private readonly OperationalDemoService _demo;
    private readonly IAuditTrailService _auditTrail;
    private readonly ILogger<JuridicoController> _logger;
    public JuridicoController(OperationalDemoService demo, IAuditTrailService auditTrail, ILogger<JuridicoController> logger) { _demo = demo; _auditTrail = auditTrail; _logger = logger; }
    [Route("/Juridico")]
    [Route("/Juridico/Processos")]
    [Route("/Juridico/Prazos")]
    [Route("/Juridico/Pareceres")]
    [Route("/Juridico/Audiencias")]
    public IActionResult Index(string? q = null) => View("~/Views/Operational/Module.cshtml", _demo.Build("Juridico", RouteData.Values["action"]?.ToString() ?? "Dashboard", q));
    [Route("/Juridico/Detalhes/{id:long}")]
    public async Task<IActionResult> Detalhes(long id, CancellationToken cancellationToken)
    {
        try { await _auditTrail.RegistrarAsync(null, null, "juridico.visualizar", "processo_juridico", id.ToString(), null, null, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString(), HttpContext.TraceIdentifier, cancellationToken); }
        catch (Exception ex) { _logger.LogWarning(ex, "Auditoria de visualização jurídica falhou"); }
        return View("~/Views/Operational/Module.cshtml", _demo.Build("Juridico", $"Detalhes #{id}"));
    }
}
