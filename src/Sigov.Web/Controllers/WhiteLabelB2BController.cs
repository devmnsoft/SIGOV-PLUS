using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class WhiteLabelB2BController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;
    private readonly ILogger<WhiteLabelB2BController> _logger;

    public WhiteLabelB2BController(IWhiteLabelB2BLaunchService service, ILogger<WhiteLabelB2BController> logger)
    {
        _service = service;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _service.GetWhiteLabelAsync(GetTenantId(), cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    public Task<IActionResult> Edit(CancellationToken cancellationToken) => Index(cancellationToken);

    public Task<IActionResult> Preview(CancellationToken cancellationToken) => Index(cancellationToken);

    public Task<IActionResult> Assets(CancellationToken cancellationToken) => Index(cancellationToken);

    public Task<IActionResult> Emails(CancellationToken cancellationToken) => Index(cancellationToken);

    public Task<IActionResult> Dominios(CancellationToken cancellationToken) => Index(cancellationToken);

    public Task<IActionResult> Publicar(CancellationToken cancellationToken) => Index(cancellationToken);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Salvar(WhiteLabelAtualizarRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await _service.AtualizarWhiteLabelAsync(GetTenantId(), request, GetUserId(), cancellationToken).ConfigureAwait(false);
            TempData["Toast"] = "White label atualizado com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar white label via Web.");
            TempData["ToastErro"] = "Não foi possível salvar a configuração.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmarPublicacao(CancellationToken cancellationToken)
    {
        await _service.PublicarWhiteLabelAsync(GetTenantId(), GetUserId(), cancellationToken).ConfigureAwait(false);
        TempData["Toast"] = "White label publicado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestaurarPadrao(CancellationToken cancellationToken)
    {
        await _service.RestaurarWhiteLabelPadraoAsync(GetTenantId(), GetUserId(), cancellationToken).ConfigureAwait(false);
        TempData["Toast"] = "White label restaurado para o padrão.";
        return RedirectToAction(nameof(Index));
    }

    private long GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value ?? Request.Headers["X-Tenant"].FirstOrDefault();
        return long.TryParse(claim, out var tenantId) && tenantId > 0 ? tenantId : 1;
    }

    private long? GetUserId()
    {
        var claim = User.FindFirst("sub")?.Value ?? User.FindFirst("usuario_id")?.Value;
        return long.TryParse(claim, out var userId) ? userId : null;
    }
}
