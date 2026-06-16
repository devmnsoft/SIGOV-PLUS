using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
public sealed class RelatoriosController : Controller
{
    private readonly ILogger<RelatoriosController> _logger;
    public RelatoriosController(ILogger<RelatoriosController> logger) => _logger = logger;
    [Route("/Relatorios")]
    public IActionResult Index() { try { return View(); } catch (Exception ex) { _logger.LogError(ex, "Falha relatórios"); TempData["Error"]="Relatórios indisponíveis."; return View(); } }
}
