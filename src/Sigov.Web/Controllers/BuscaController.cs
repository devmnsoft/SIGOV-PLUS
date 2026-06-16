using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
public sealed class BuscaController : Controller
{
    private readonly ILogger<BuscaController> _logger;
    public BuscaController(ILogger<BuscaController> logger) => _logger = logger;
    [Route("/Busca")]
    public IActionResult Index(string? q) { try { ViewBag.Query = q ?? string.Empty; return View(); } catch (Exception ex) { _logger.LogError(ex, "Falha busca global"); ViewBag.Query = q ?? string.Empty; return View(); } }
}
