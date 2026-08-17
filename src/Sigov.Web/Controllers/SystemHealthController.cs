using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Health;

namespace Sigov.Web.Controllers;

[Authorize]
public sealed class SystemHealthController : Controller
{
    private readonly ProjectStatusProvider _provider;
    public SystemHealthController(ProjectStatusProvider provider) => _provider = provider;

    [HttpGet("/SystemHealth/ProjectStatus")]
    public async Task<IActionResult> ProjectStatus(CancellationToken cancellationToken) =>
        View(await _provider.GetAsync(cancellationToken).ConfigureAwait(false));
}
