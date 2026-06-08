using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class MonitoramentoB2BController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;

    public MonitoramentoB2BController(IWhiteLabelB2BLaunchService service) => _service = service;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _service.GetMonitoramentoAsync(cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    public Task<IActionResult> Tenants(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Performance(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Erros(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Alertas(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> HealthChecks(CancellationToken cancellationToken) => Index(cancellationToken);
}
