using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger) => _logger = logger;

    public IActionResult Index() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Dashboard") : RedirectToAction("Login", "Auth");

    [Route("Home/Error/{code:int?}")]
    public IActionResult Error(int? code)
    {
        _logger.LogWarning("Página de erro amigável exibida. Code={Code} CorrelationId={CorrelationId}", code, HttpContext.TraceIdentifier);
        Response.StatusCode = code ?? StatusCodes.Status500InternalServerError;
        ViewData["Code"] = Response.StatusCode;
        return View();
    }

    [Route("Home/SessaoExpirada")]
    public IActionResult SessaoExpirada() => View();
}
