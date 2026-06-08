using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class GoToMarketController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;
    public GoToMarketController(IWhiteLabelB2BLaunchService service) => _service = service;
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.GetMateriaisGoToMarketAsync("interno", cancellationToken).ConfigureAwait(false));
    public Task<IActionResult> CasosUso(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Materiais(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Campanhas(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Decisores(CancellationToken cancellationToken) => Index(cancellationToken);
}
