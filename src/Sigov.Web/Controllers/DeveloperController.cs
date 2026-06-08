using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Saas.B2B;

namespace Sigov.Web.Controllers;

public sealed class DeveloperController : Controller
{
    private readonly IWhiteLabelB2BLaunchService _service;

    public DeveloperController(IWhiteLabelB2BLaunchService service) => _service = service;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await _service.GetDeveloperOverviewAsync(GetTenantId(), cancellationToken).ConfigureAwait(false);
        return View(model);
    }

    public Task<IActionResult> Autenticacao(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Endpoints(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Webhooks(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> ApiKeys(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> RateLimits(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Exemplos(CancellationToken cancellationToken) => Index(cancellationToken);
    public Task<IActionResult> Uso(CancellationToken cancellationToken) => Index(cancellationToken);

    private long GetTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value ?? Request.Headers["X-Tenant"].FirstOrDefault();
        return long.TryParse(claim, out var tenantId) && tenantId > 0 ? tenantId : 1;
    }
}
