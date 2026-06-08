using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class PlanosPublicosController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;

    public PlanosPublicosController(IWhiteLabelB2BLaunchService service) => _service = service;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _service.GetPlanosPublicosAsync(cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    public async Task<IActionResult> Comparar(CancellationToken cancellationToken)
    {
        var model = await _service.GetComparativoAsync(cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    public IActionResult Sla() => View();
}
