using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class ContratosB2BController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;
    public ContratosB2BController(IWhiteLabelB2BLaunchService service) => _service = service;
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(await _service.GetContratosAsync(null, cancellationToken).ConfigureAwait(false));
    public Task<IActionResult> Create(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Edit(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Details(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Sla(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Aceites(CancellationToken cancellationToken) => Index(cancellationToken);
}
