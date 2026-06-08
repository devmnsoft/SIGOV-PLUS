using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class SelfServiceController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;
    private readonly ILogger<SelfServiceController> _logger;

    public SelfServiceController(IWhiteLabelB2BLaunchService service, ILogger<SelfServiceController> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Planos = await _service.GetPlanosPublicosAsync(cancellationToken).ConfigureAwait(false);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cadastrar(SelfServiceCadastroRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.SolicitarCadastroAsync(request, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers["User-Agent"].FirstOrDefault(), cancellationToken).ConfigureAwait(false);
            TempData[result.Success ? "Toast" : "ToastErro"] = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no cadastro self-service Web.");
            TempData["ToastErro"] = "Não foi possível concluir o cadastro agora.";
        }

        return RedirectToAction(nameof(Index));
    }
}
