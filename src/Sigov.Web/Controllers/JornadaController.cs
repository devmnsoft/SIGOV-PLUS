using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class JornadaController : Controller
{
    private readonly ILogger<JornadaController> _logger;
    public JornadaController(ILogger<JornadaController> logger) => _logger = logger;

    [HttpGet]
    public IActionResult Index()
    {
        _logger.LogInformation("Jornada SIGOV PLUS acessada. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
        return View();
    }
}
