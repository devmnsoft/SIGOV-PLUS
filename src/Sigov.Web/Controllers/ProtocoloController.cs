using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;

namespace Sigov.Web.Controllers;

public sealed class ProtocoloController : Controller
{
    private readonly OperationalDemoService _demo;
    private readonly ILogger<ProtocoloController> _logger;
    public ProtocoloController(OperationalDemoService demo, ILogger<ProtocoloController> logger) { _demo = demo; _logger = logger; }
    [Route("/Protocolo")]
    [Route("/Protocolo/Processos")]
    [Route("/Protocolo/Novo")]
    [Route("/Protocolo/Tramitar")]
    [Route("/Protocolo/MinhasPendencias")]
    public IActionResult Index(string? q = null) { try { return View("~/Views/Operational/Module.cshtml", _demo.Build("Protocolo", RouteData.Values["action"]?.ToString() ?? "Dashboard", q)); } catch (Exception ex) { _logger.LogError(ex, "Falha Protocolo"); return View("~/Views/Operational/Module.cshtml", _demo.Build("Protocolo")); } }
    [Route("/Protocolo/Detalhes/{id:long}")]
    public IActionResult Detalhes(long id) => View("~/Views/Operational/Module.cshtml", _demo.Build("Protocolo", $"Detalhes #{id}"));
}
