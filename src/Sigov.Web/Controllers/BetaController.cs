using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class BetaController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;
    public BetaController(IWhiteLabelB2BLaunchService service) => _service = service;
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.GetBetaFeedbacksAsync(null, cancellationToken).ConfigureAwait(false));
    public Task<IActionResult> Programas(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Clientes(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Feedbacks(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Indicadores(CancellationToken cancellationToken) => Index(cancellationToken);
}
