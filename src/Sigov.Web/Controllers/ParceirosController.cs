using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class ParceirosController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;
    public ParceirosController(IWhiteLabelB2BLaunchService service) => _service = service;
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.GetMateriaisGoToMarketAsync("parceiro", cancellationToken).ConfigureAwait(false));
    public Task<IActionResult> Details(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Tenants(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Comissoes(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Repasses(CancellationToken cancellationToken) => Index(cancellationToken);
}
