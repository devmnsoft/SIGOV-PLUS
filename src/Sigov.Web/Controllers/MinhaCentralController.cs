using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class MinhaCentralController : Controller
{
    private readonly ILogger<MinhaCentralController> _logger;
    public MinhaCentralController(ILogger<MinhaCentralController> logger) => _logger = logger;

    [HttpGet]
    public IActionResult Index()
    {
        try
        {
            ViewData["Perfil"] = User.IsInRole("ADMINISTRADOR_GERAL") ? "Administrador Geral" : "Operador";
            ViewData["Tenant"] = User.FindFirst("tenant_id")?.Value is { Length: > 0 } tenant ? $"Tenant #{tenant}" : "Ambiente demonstração";
            _logger.LogInformation("Minha Central acessada. Usuario={Usuario} CorrelationId={CorrelationId}", User.Identity?.Name, HttpContext.TraceIdentifier);
            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha tratada ao abrir Minha Central. CorrelationId={CorrelationId}", HttpContext.TraceIdentifier);
            TempData["Warning"] = "Abrimos sua central em modo seguro porque alguns dados do ambiente estão indisponíveis.";
            return View();
        }
    }
}
