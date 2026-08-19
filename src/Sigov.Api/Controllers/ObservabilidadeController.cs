using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigov.Infrastructure.Health;

namespace Sigov.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/observabilidade")]
public sealed class ObservabilidadeController : ControllerBase
{
    private readonly ProjectStatusProvider _provider;
    public ObservabilidadeController(ProjectStatusProvider provider) => _provider = provider;

    [HttpGet("health")]
    [HttpGet("liveness")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new
    {
        status = "healthy",
        timestampUtc = DateTimeOffset.UtcNow
    });

    [HttpGet("dashboard")]
    [HttpGet("readiness")]
    [HttpGet("migrations")]
    [HttpGet("modulos")]
    [HttpGet("rotas")]
    [HttpGet("validadores")]
    [HttpGet("seguranca")]
    [HttpGet("workers")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken) =>
        Ok(await _provider.GetAsync(cancellationToken).ConfigureAwait(false));
}
