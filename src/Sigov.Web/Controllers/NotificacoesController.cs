using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Web.Services;
namespace Sigov.Web.Controllers;
[Authorize]
public sealed class NotificacoesController : Controller
{
    private readonly PostBuildSaasService _service;
    private readonly ILogger<NotificacoesController> _logger;
    public NotificacoesController(PostBuildSaasService service, ILogger<NotificacoesController> logger) { _service = service; _logger = logger; }
    [HttpGet("/Notificacoes")]
    public async Task<IActionResult> Index(string? status, CancellationToken cancellationToken) { try { return View(await _service.ListarNotificacoesAsync(status, cancellationToken).ConfigureAwait(false)); } catch (Exception ex) { _logger.LogError(ex, "Falha notificações"); return View(new Sigov.Web.Models.PostBuild.SaasNotificationsViewModel { MensagemFallback = "Não foi possível carregar notificações." }); } }
}
