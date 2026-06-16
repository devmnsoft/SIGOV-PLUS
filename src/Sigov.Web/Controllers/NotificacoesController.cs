using Microsoft.AspNetCore.Mvc;
namespace Sigov.Web.Controllers;
public sealed class NotificacoesController : Controller
{
    private readonly ILogger<NotificacoesController> _logger;
    public NotificacoesController(ILogger<NotificacoesController> logger) => _logger = logger;
    [Route("/Notificacoes")]
    public IActionResult Index(string? status = null) { try { ViewBag.Status = status; return View(); } catch (Exception ex) { _logger.LogError(ex, "Falha notificações"); return View(); } }
}
