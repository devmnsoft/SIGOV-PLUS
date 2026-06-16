using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class ManualController : Controller
{
    private readonly ILogger<ManualController> _logger;
    public ManualController(ILogger<ManualController> logger) => _logger = logger;

    [HttpGet]
    public IActionResult Index(string? busca)
    {
        ViewData["Busca"] = busca ?? string.Empty;
        _logger.LogInformation("Manual do sistema acessado. Busca={Busca} CorrelationId={CorrelationId}", busca, HttpContext.TraceIdentifier);
        return View();
    }
}
