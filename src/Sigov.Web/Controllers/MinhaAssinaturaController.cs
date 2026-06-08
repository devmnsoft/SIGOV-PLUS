using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class MinhaAssinaturaController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;

    public MinhaAssinaturaController(IWhiteLabelB2BLaunchService service) => _service = service;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _service.GetUsoAssinaturaAsync(GetTenantId(), cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    public Task<IActionResult> Uso(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Faturas(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Upgrade(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Downgrade(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Cancelamento(CancellationToken cancellationToken) => Index(cancellationToken);

    private long GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value ?? Request.Headers["X-Tenant"].FirstOrDefault();
        return long.TryParse(claim, out var tenantId) && tenantId > 0 ? tenantId : 1;
    }
}
