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

    [HttpGet("dashboard")]
    [HttpGet("health")]
    [HttpGet("migrations")]
    [HttpGet("modulos")]
    [HttpGet("rotas")]
    [HttpGet("validadores")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken) =>
        Ok(await _provider.GetAsync(cancellationToken).ConfigureAwait(false));
}
