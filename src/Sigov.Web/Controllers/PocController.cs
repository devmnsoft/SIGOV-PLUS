using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
public sealed class PocController : Controller
{
    private readonly ILogger<PocController> _logger;
    public PocController(ILogger<PocController> logger) => _logger = logger;
    [Route("/Poc")]
    public IActionResult Index() { try { return View(); } catch (Exception ex) { _logger.LogError(ex, "Falha POC"); return View(); } }
}
