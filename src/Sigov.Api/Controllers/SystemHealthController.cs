using Microsoft.AspNetCore.Mvc;
using Sigov.Application.Health;
using Sigov.Infrastructure.Health;

namespace Sigov.Api.Controllers;

[ApiController]
[Route("api/system-health")]
public sealed class SystemHealthController : ControllerBase
{
    private readonly ProjectStatusProvider _provider;
    public SystemHealthController(ProjectStatusProvider provider) => _provider = provider;

    [HttpGet("project-status")]
    [ProducesResponseType(typeof(ProjectStatusResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProjectStatusResponse>> ProjectStatus(CancellationToken cancellationToken) =>
        Ok(await _provider.GetAsync(cancellationToken).ConfigureAwait(false));
}
